using System.Reflection;

namespace ResumeBuilder.Core.Services;

/// <summary>Сведения о программе. Версия берётся из сборки, а туда — из Directory.Build.props.</summary>
public static class AppInfo
{
    private static Assembly MainAssembly => Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;

    /// <summary>Версия программы, например 1.2.0.</summary>
    public static Version Version
    {
        get
        {
            Version v = MainAssembly.GetName().Version ?? new Version(1, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    /// <summary>Название продукта.</summary>
    public static string Product =>
        MainAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "ResumeBuilder";

    /// <summary>Строка авторских прав.</summary>
    public static string Copyright =>
        MainAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
}
