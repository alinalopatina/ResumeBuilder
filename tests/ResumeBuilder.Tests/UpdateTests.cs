using ResumeBuilder.Core.Services;
using Xunit;

namespace ResumeBuilder.Tests;

/// <summary>Проверка обновлений: разбор версии и ответа GitHub (без обращения к сети).</summary>
public class UpdateTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.4", "1.4.0")]
    [InlineData(" v2.0.10-beta ", "2.0.10")]
    public void ParseVersion_FromTag(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateService.ParseVersion(tag));
    }

    [Theory]
    [InlineData("")]
    [InlineData("release")]
    [InlineData(null)]
    public void ParseVersion_BadTag_IsNull(string? tag)
    {
        Assert.Null(UpdateService.ParseVersion(tag));
    }

    [Fact]
    public void ParseRelease_FindsInstaller()
    {
        const string json = """
            {
              "tag_name": "v1.3.0",
              "html_url": "https://github.com/alinalopatina/ResumeBuilder/releases/tag/v1.3.0",
              "assets": [
                { "name": "ResumeBuilder-1.3.0-portable-win-x64.zip", "browser_download_url": "https://example.com/a.zip" },
                { "name": "ResumeBuilder-Setup-1.3.0.exe", "browser_download_url": "https://example.com/setup.exe" }
              ]
            }
            """;
        UpdateInfo info = UpdateService.ParseRelease(json);

        Assert.Equal(new Version(1, 3, 0), info.Version);
        Assert.Equal("https://example.com/setup.exe", info.InstallerUrl);
        Assert.True(UpdateService.IsNewer(info, new Version(1, 2, 0)));
        Assert.False(UpdateService.IsNewer(info, new Version(1, 3, 0)));
    }

    [Fact]
    public void ParseRelease_NoTag_Throws()
    {
        Assert.Throws<InvalidDataException>(() => UpdateService.ParseRelease("""{ "name": "без тега" }"""));
    }

    [Fact]
    public void AppVersion_FromDirectoryBuildProps()
    {
        // Все сборки решения получают версию из одного файла — Directory.Build.props
        Version? version = typeof(AppInfo).Assembly.GetName().Version;
        Assert.NotNull(version);
        Assert.True(version >= new Version(1, 2, 0));
    }
}
