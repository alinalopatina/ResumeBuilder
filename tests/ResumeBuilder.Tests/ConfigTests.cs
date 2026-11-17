using System.Text.Json;
using ResumeBuilder.Core.Configuration;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>Конфигурирование (лаба 9): иерархия источников, профили, проверка, импорт/экспорт.</summary>
public class ConfigTests : IClassFixture<TestFolderFixture>
{
    private readonly string _dir;      // «папка программы» с appsettings.json
    private readonly string _userFile; // личные настройки

    /// <summary>Перед каждым тестом — своя чистая папка.</summary>
    public ConfigTests(TestFolderFixture folder)
    {
        _dir = Path.Combine(folder.Folder, "config_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _userFile = Path.Combine(_dir, "user", "usersettings.json");
    }

    private void WriteJson(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private AppConfig Load(params string[] args) =>
        ConfigManager.Bind(ConfigManager.Build(_dir, _userFile, args, "RBTEST_NOTHING_"));

    [Fact]
    public void NoFiles_DefaultsFromCode()
    {
        AppConfig config = Load();

        Assert.Equal("Production", config.Environment);
        Assert.Equal(8, config.QrPixelsPerModule);
        Assert.Equal("#4F46E5", config.AccentColor);
    }

    [Fact]
    public void Priority_AppFile_UserFile_CommandLine()
    {
        WriteJson(Path.Combine(_dir, "appsettings.json"), """{ "App": { "QrPixelsPerModule": 9, "AutosaveMinutes": 3 } }""");
        Assert.Equal(9, Load().QrPixelsPerModule);                   // файл приложения перекрывает код

        WriteJson(_userFile, """{ "App": { "QrPixelsPerModule": 10 } }""");
        AppConfig config = Load();
        Assert.Equal(10, config.QrPixelsPerModule);                  // личный файл перекрывает файл приложения
        Assert.Equal(3, config.AutosaveMinutes);                     // чего нет в личном файле — из файла приложения

        Assert.Equal(12, Load("--App:QrPixelsPerModule=12").QrPixelsPerModule); // аргументы — самый высокий приоритет
    }

    [Fact]
    public void EnvironmentVariable_BetweenFilesAndArguments()
    {
        string prefix = "RBTEST_" + Guid.NewGuid().ToString("N") + "_";
        WriteJson(_userFile, """{ "App": { "AutosaveMinutes": 10 } }""");
        Environment.SetEnvironmentVariable(prefix + "App__AutosaveMinutes", "20");
        try
        {
            AppConfig fromEnv = ConfigManager.Bind(ConfigManager.Build(_dir, _userFile, Array.Empty<string>(), prefix));
            Assert.Equal(20, fromEnv.AutosaveMinutes);

            AppConfig fromArgs = ConfigManager.Bind(ConfigManager.Build(_dir, _userFile, new[] { "--App:AutosaveMinutes=30" }, prefix));
            Assert.Equal(30, fromArgs.AutosaveMinutes);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefix + "App__AutosaveMinutes", null);
        }
    }

    [Fact]
    public void Profile_ChosenByShortArgument()
    {
        WriteJson(Path.Combine(_dir, "appsettings.json"), """{ "App": { "AccentColor": "#111111" } }""");
        WriteJson(Path.Combine(_dir, "appsettings.Development.json"), """{ "App": { "AccentColor": "#D97706" } }""");

        Assert.Equal("#111111", Load().AccentColor);

        AppConfig dev = Load("--env", "Development");
        Assert.Equal("Development", dev.Environment);
        Assert.Equal("#D97706", dev.AccentColor);
    }

    [Fact]
    public void Validate_DefaultsAreValid()
    {
        Assert.Empty(new AppConfig().Validate());
    }

    [Fact]
    public void Validate_FindsAllWrongValues()
    {
        var config = new AppConfig
        {
            Environment = "Test",
            DefaultLanguage = "de-DE",
            QrPixelsPerModule = 50,
            AutosaveMinutes = -1,
            AccentColor = "красный",
            SlowOperationMs = 0,
            ExportFolder = Path.Combine(_dir, "нет такой папки")
        };

        Assert.Equal(7, config.Validate().Count);
    }

    [Fact]
    public void SaveUserSettings_WritesOnlyChangedValues()
    {
        WriteJson(Path.Combine(_dir, "appsettings.json"), """{ "App": { "QrPixelsPerModule": 9 } }""");
        var config = new AppConfig { QrPixelsPerModule = 9, AccentColor = "#000000" };

        int count = ConfigManager.SaveUserSettings(config, _dir, _userFile);

        // QR-код совпадает с appsettings.json — в личный файл не пишется
        Assert.Equal(1, count);
        string json = File.ReadAllText(_userFile);
        Assert.Contains("AccentColor", json);
        Assert.DoesNotContain("QrPixelsPerModule", json);
        Assert.Equal("#000000", Load().AccentColor);
    }

    [Fact]
    public void ExportThenImport_SameValues()
    {
        string file = Path.Combine(_dir, "export.json");
        var original = new AppConfig { QrPixelsPerModule = 15, AccentColor = "#123456", AutosaveMinutes = 0 };

        ConfigManager.SaveToFile(original, file);
        AppConfig imported = ConfigManager.LoadFromFile(file, new AppConfig());

        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(imported));
    }

    [Fact]
    public void Import_BadFiles_Throw()
    {
        string broken = Path.Combine(_dir, "broken.json");
        File.WriteAllText(broken, "{ это не json");

        Assert.Throws<InvalidDataException>(() => ConfigManager.LoadFromFile(broken, new AppConfig()));
        Assert.Throws<FileNotFoundException>(() => ConfigManager.LoadFromFile(Path.Combine(_dir, "nope.json"), new AppConfig()));
    }
}
