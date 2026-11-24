using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

namespace ResumeBuilder.Core.Services;

/// <summary>Последний выпуск программы на GitHub.</summary>
/// <param name="Version">Версия из тега, например 1.3.0.</param>
/// <param name="Tag">Тег выпуска, например v1.3.0.</param>
/// <param name="PageUrl">Страница выпуска в браузере.</param>
/// <param name="InstallerUrl">Ссылка на установщик (*.exe) или null, если его нет.</param>
public record UpdateInfo(Version Version, string Tag, string PageUrl, string? InstallerUrl);

/// <summary>
/// Проверка обновлений (лаба 10.1, задание 2.3): программа спрашивает у GitHub последний выпуск,
/// сравнивает его версию со своей и при необходимости скачивает установщик.
/// Установщик ставится поверх старой версии (одинаковый AppId в Inno Setup).
/// </summary>
public static class UpdateService
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UpdateService));
    private static readonly Regex VersionRegex = new(@"^v?(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.Compiled);

    /// <summary>Разбирает версию из тега: "v1.2.3" → 1.2.3. Неверный тег → null.</summary>
    public static Version? ParseVersion(string? tag)
    {
        Match m = VersionRegex.Match(tag?.Trim() ?? "");
        if (!m.Success)
            return null;
        int build = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), build);
    }

    /// <summary>Разбирает ответ GitHub API (releases/latest).</summary>
    /// <exception cref="InvalidDataException">В ответе нет тега с версией.</exception>
    public static UpdateInfo ParseRelease(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string tag = root.TryGetProperty("tag_name", out JsonElement t) ? t.GetString() ?? "" : "";
        Version version = ParseVersion(tag) ?? throw new InvalidDataException("В выпуске нет версии: " + tag);
        string page = root.TryGetProperty("html_url", out JsonElement h) ? h.GetString() ?? "" : "";

        string? installer = null;
        if (root.TryGetProperty("assets", out JsonElement assets))
        {
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    installer = asset.GetProperty("browser_download_url").GetString();
            }
        }
        return new UpdateInfo(version, tag, page, installer);
    }

    /// <summary>Новее ли выпуск, чем текущая версия.</summary>
    public static bool IsNewer(UpdateInfo release, Version current) => release.Version > current;

    /// <summary>Узнаёт последний выпуск на GitHub.</summary>
    /// <param name="repository">Репозиторий в виде "владелец/имя".</param>
    /// <param name="client">HTTP-клиент; если не передан — создаётся новый.</param>
    /// <returns>Сведения о выпуске или null, если выпусков ещё нет.</returns>
    /// <exception cref="HttpRequestException">Нет интернета или GitHub недоступен.</exception>
    public static async Task<UpdateInfo?> GetLatestAsync(string repository, HttpClient? client = null)
    {
        client ??= CreateClient();
        string url = $"https://api.github.com/repos/{repository}/releases/latest";
        using HttpResponseMessage response = await client.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Logger.Information("Выпусков в {Repository} пока нет", repository);
            return null;
        }
        response.EnsureSuccessStatusCode();
        UpdateInfo info = ParseRelease(await response.Content.ReadAsStringAsync());
        Logger.Information("Последний выпуск {Tag}, текущая версия {Current}", info.Tag, AppInfo.Version);
        return info;
    }

    /// <summary>Скачивает установщик во временную папку и возвращает путь к файлу.</summary>
    public static async Task<string> DownloadAsync(string url, HttpClient? client = null)
    {
        client ??= CreateClient();
        string path = Path.Combine(Path.GetTempPath(), Path.GetFileName(new Uri(url).LocalPath));
        byte[] bytes = await client.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(path, bytes);
        Logger.Information("Установщик скачан: {Path} ({Size} байт)", path, bytes.Length);
        return path;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // GitHub API требует заголовок User-Agent
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ResumeBuilder", AppInfo.Version.ToString()));
        return client;
    }
}
