using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Reports;
using ResumeBuilder.Core.Text;
using ResumeBuilder.Core.Validation;
using ResumeBuilder.Infrastructure.Logging;
using ResumeBuilder.Infrastructure.Office;
using ResumeBuilder.Infrastructure.Pdf;
using ResumeBuilder.Infrastructure.Qr;
using ResumeBuilder.Infrastructure.Reports;
using ResumeBuilder.Infrastructure.Serialization;
using ResumeBuilder.Infrastructure.Settings;
using Serilog;

namespace ResumeBuilder.Infrastructure;

/// <summary>
/// Корень композиции: регистрация всех сервисов в контейнере внедрения зависимостей (DI).
/// И WPF-приложение, и консольная утилита вызывают один и тот же метод.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>Загружает appsettings.json (+ appsettings.{Environment}.json и переменные окружения).</summary>
    public static IConfiguration BuildConfiguration(string? basePath = null)
    {
        var environment = Environment.GetEnvironmentVariable("RESUMEBUILDER_ENVIRONMENT") ?? "Production";
        return new ConfigurationBuilder()
            .SetBasePath(basePath ?? AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("RESUMEBUILDER_")
            .Build();
    }

    /// <summary>Регистрирует сервисы приложения.</summary>
    public static IServiceCollection AddResumeBuilder(this IServiceCollection services, IConfiguration configuration)
    {
        // Журналирование: Serilog как провайдер для Microsoft.Extensions.Logging
        Log.Logger = LoggingSetup.CreateMainLogger(configuration);
        services.AddSingleton(configuration);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            builder.AddSerilog(Log.Logger, dispose: true);
        });
        services.AddSingleton<IAuditLogger>(new SerilogAuditLogger(LoggingSetup.CreateAuditLogger(configuration)));

        // Локализация
        var uiCulture = configuration["Localization:DefaultCulture"] ?? "ru-RU";
        services.AddSingleton<ILocalizer>(new ResxLocalizer(CultureInfo.GetCultureInfo(uiCulture)));

        // Валидация (стратегии + компоновщик)
        services.AddSingleton<ManualResumeValidator>();
        services.AddSingleton<AnnotationResumeValidator>();
        services.AddSingleton<IResumeValidator>(sp => new CompositeResumeValidator(
            sp.GetRequiredService<AnnotationResumeValidator>(),
            sp.GetRequiredService<ManualResumeValidator>()));
        services.AddSingleton<TextExtractor>();

        // Сериализация
        services.AddSingleton<IResumeSerializer, JsonResumeSerializer>();
        services.AddSingleton<IResumeSerializer, BinaryResumeSerializer>();
        services.AddSingleton<JsonResumeSerializer>();
        services.AddSingleton<BinaryResumeSerializer>();
        services.AddSingleton<ResumeFileService>();
        services.AddSingleton<AppSettingsStore>(sp => new AppSettingsStore(
            sp.GetRequiredService<ILogger<AppSettingsStore>>(), configuration["Storage:DataFolder"]));

        // Экспорт
        services.AddSingleton<IQrCodeGenerator, QrCodeService>();
        services.AddSingleton<PdfResumeExporter>();
        services.AddSingleton<WordResumeExporter>();
        services.AddSingleton<ExcelExperienceExporter>();
        services.AddSingleton<IResumeExporter>(sp => sp.GetRequiredService<PdfResumeExporter>());
        services.AddSingleton<IResumeExporter>(sp => sp.GetRequiredService<WordResumeExporter>());
        services.AddSingleton<IResumeExporter>(sp => sp.GetRequiredService<ExcelExperienceExporter>());
        services.AddSingleton<IExperienceImporter, WordExperienceImporter>();

        // Сводный отчёт: одна агрегация — несколько рендереров
        services.AddSingleton(_ => new ResumeStatisticsService());
        services.AddSingleton<ReportDocumentBuilder>();
        services.AddSingleton<IReportRenderer, ExcelReportRenderer>();
        services.AddSingleton<IReportRenderer, WordReportRenderer>();
        services.AddSingleton<IReportRenderer, PdfReportRenderer>();
        services.AddSingleton<IReportRenderer, TextReportRenderer>();
        services.AddSingleton<ReportService>();

        return services;
    }
}
