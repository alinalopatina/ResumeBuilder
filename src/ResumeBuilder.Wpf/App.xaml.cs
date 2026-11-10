using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Infrastructure;
using ResumeBuilder.Infrastructure.Settings;
using ResumeBuilder.Wpf.Services;
using ResumeBuilder.Wpf.ViewModels;

namespace ResumeBuilder.Wpf;

/// <summary>
/// Точка входа WPF-приложения: собирает DI-контейнер, настраивает журнал и язык, показывает главное окно.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private ILogger<App>? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = ServiceRegistration.BuildConfiguration();
        var services = new ServiceCollection()
            .AddResumeBuilder(configuration)
            .AddSingleton<IDialogService, DialogService>()
            .AddSingleton<PrintService>()
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("Приложение запущено. Версия {Version}", typeof(App).Assembly.GetName().Version);

        // Глобальный перехват необработанных исключений — пишем в журнал вместо «тихого» падения
        DispatcherUnhandledException += OnUnhandledException;

        // Язык интерфейса: из сохранённых настроек пользователя
        var settings = _services.GetRequiredService<AppSettingsStore>().Load();
        var culture = CultureInfo.GetCultureInfo(settings.UiCulture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LocalizationManager.Instance.Attach(_services.GetRequiredService<ILocalizer>());
        LocalizationManager.Instance.SetCulture(culture);

        // WPF по умолчанию форматирует числа/даты в привязках как en-US — переключаем на культуру пользователя
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("Приложение завершено с кодом {Code}", e.ApplicationExitCode);
        _services?.Dispose(); // сбрасывает буферы Serilog на диск
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.Exception, "Необработанное исключение");
        MessageBox.Show(LocalizationManager.Instance.Localizer.Format("Error_Generic", e.Exception.Message),
            LocalizationManager.Instance["App_Title"], MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
