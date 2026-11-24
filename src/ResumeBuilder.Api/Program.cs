using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ResumeBuilder.Api.Storage;
using ResumeBuilder.Core.Configuration;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using ResumeBuilder.Core.Validation;
using Serilog;

// ResumeBuilder.Api — веб-служба на основе той же библиотеки ResumeBuilder.Core.
// Все настройки — из appsettings.json и ПЕРЕМЕННЫХ ОКРУЖЕНИЯ (лаба 10.2, задание 1.3):
//   ASPNETCORE_HTTP_PORTS=8080                  — порт
//   ConnectionStrings__Postgres=Host=db;...     — база (пусто — хранение в памяти)
//   ConnectionStrings__Redis=cache:6379         — кэш (пусто — кэш в памяти)
//   App__QrPixelsPerModule=10, App__DefaultLanguage=en-US

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Журнал библиотеки Core (Serilog) — в консоль, его видно в «docker logs»
Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Console().CreateLogger();

// Настройки ядра из секции "App"
AppConfig appConfig = ConfigManager.Bind(builder.Configuration);
List<string> configErrors = appConfig.Validate();
if (configErrors.Count > 0)
    throw new InvalidOperationException("Ошибка в настройках: " + string.Join("; ", configErrors));
ConfigManager.Apply(appConfig);

// Хранилище: PostgreSQL, если задана строка подключения, иначе — память
string? postgres = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgres))
    builder.Services.AddSingleton<IResumeRepository, InMemoryResumeRepository>();
else
    builder.Services.AddSingleton<IResumeRepository>(sp =>
        new PostgresResumeRepository(postgres, sp.GetRequiredService<ILogger<PostgresResumeRepository>>()));

// Кэш готовых PDF: Redis, если задан, иначе — память процесса
string? redis = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redis))
    builder.Services.AddDistributedMemoryCache();
else
    builder.Services.AddStackExchangeRedisCache(o => { o.Configuration = redis; o.InstanceName = "resumebuilder:"; });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

// Язык ответов: ?culture=en-US или по умолчанию из App:DefaultLanguage
string[] cultures = { "ru-RU", "en-US" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(appConfig.DefaultLanguage)
    .AddSupportedCultures(cultures)
    .AddSupportedUICultures(cultures));

app.UseSwagger();
app.UseSwaggerUI();

IResumeRepository repository = app.Services.GetRequiredService<IResumeRepository>();
await repository.InitAsync();
string cacheName = string.IsNullOrWhiteSpace(redis) ? "memory" : "redis";
app.Logger.LogInformation("ResumeBuilder.Api {Version}: хранилище {Storage}, кэш {Cache}",
    AppInfo.Version, repository.Name, cacheName);

// ---------- служебные ----------

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    version = AppInfo.Version.ToString(),
    storage = repository.Name,
    cache = cacheName,
    host = Environment.MachineName   // видно, какая реплика ответила (Swarm / Kubernetes)
}));

// ---------- резюме без сохранения ----------

app.MapGet("/api/sample", () => Resume.CreateSample())
   .WithSummary("Резюме-пример (можно отправить в другие методы)");

app.MapPost("/api/validate", (Resume resume) => Results.Ok(ResumeValidator.Validate(resume)))
   .WithSummary("Проверка резюме: список ошибок (пустой — ошибок нет)");

app.MapPost("/api/pdf", async (Resume resume, IDistributedCache cache, HttpContext http, bool qr = true) =>
{
    // Ключ кэша — хэш содержимого резюме: одинаковое резюме не собирается в PDF повторно
    string key = "pdf:" + Hash(JsonSerializer.Serialize(resume) + qr + CultureInfo.CurrentUICulture.Name);
    byte[]? pdf = await cache.GetAsync(key);
    http.Response.Headers["X-Cache"] = pdf == null ? "MISS" : "HIT";
    if (pdf == null)
    {
        pdf = BuildPdf(resume, qr);
        await cache.SetAsync(key, pdf, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
    }
    return Results.File(pdf, "application/pdf", "resume.pdf");
}).WithSummary("Резюме в PDF с QR-кодом (результат кэшируется в Redis)");

app.MapGet("/api/qr", (string text) => Results.File(QrCodeService.CreatePng(text), "image/png"))
   .WithSummary("QR-код для любого текста или ссылки");

// ---------- сохранённые резюме (PostgreSQL) ----------

app.MapPost("/api/resumes", async (Resume resume) =>
{
    List<string> errors = ResumeValidator.Validate(resume);
    if (errors.Count > 0)
        return Results.BadRequest(errors);
    int id = await repository.AddAsync(resume);
    return Results.Created($"/api/resumes/{id}", new { id });
}).WithSummary("Сохранить резюме");

app.MapGet("/api/resumes", () => repository.ListAsync()).WithSummary("Список сохранённых резюме");

app.MapGet("/api/resumes/{id:int}", async (int id) =>
    await repository.GetAsync(id) is Resume r ? Results.Ok(r) : Results.NotFound());

app.MapGet("/api/resumes/{id:int}/pdf", async (int id) =>
    await repository.GetAsync(id) is Resume r
        ? Results.File(BuildPdf(r, true), "application/pdf", $"resume-{id}.pdf")
        : Results.NotFound());

app.MapDelete("/api/resumes/{id:int}", async (int id) =>
    await repository.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

app.Run();

// PdfExporter пишет в файл — используем временный файл и сразу удаляем
static byte[] BuildPdf(Resume resume, bool qr)
{
    string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
    try
    {
        PdfExporter.Export(resume, path, qr);
        return File.ReadAllBytes(path);
    }
    finally
    {
        File.Delete(path);
    }
}

static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

