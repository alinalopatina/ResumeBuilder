using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using ResumeBuilder.Core.Configuration;
using ResumeBuilder.Core.Resources;
using ResumeBuilder.Core.Services;
using ResumeBuilder.Wpf.ViewModels;
using Serilog;

namespace ResumeBuilder.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Настройки из всех источников: код → appsettings.json → профиль → личный файл →
        //    переменные окружения → аргументы командной строки (см. ConfigManager)
        AppConfig config;
        try
        {
            config = ConfigManager.Load(e.Args);
        }
        catch (Exception ex)
        {
            // Например, в usersettings.json ошибка в JSON
            ShowConfigErrorAndExit(new List<string> { ex.Message });
            return;
        }

        // 2. Журнал читает свои настройки из той же объединённой конфигурации
        LoggingSetup.Init(ConfigManager.Root);
        Log.Information("Источники настроек:{NewLine}{Sources}", Environment.NewLine, ConfigManager.DescribeSources());

        // 3. Проверка настроек при запуске: лучше сразу сообщить об ошибке, чем работать непредсказуемо
        List<string> errors = config.Validate();
        if (errors.Count > 0)
        {
            Log.Error("Ошибки в настройках: {Errors}", string.Join("; ", errors));
            ShowConfigErrorAndExit(errors);
            return;
        }

        // 4. Язык интерфейса
        var culture = new CultureInfo(config.DefaultLanguage);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        ResumeBuilder.Wpf.Properties.Resources.Culture = culture;
        Messages.Culture = culture;

        // Чтобы даты в DatePicker отображались в формате выбранного языка
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        // 5. Основной цвет интерфейса из настроек
        ApplyAccentColor(config.AccentColor);

        AppSettings settings = AppSettings.Load();
        // Файл резюме, переданный в аргументах (двойной щелчок по .rbin после установки)
        string? fileToOpen = e.Args.FirstOrDefault(a =>
            (a.EndsWith(".rbin", StringComparison.OrdinalIgnoreCase) || a.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            && File.Exists(a));
        var viewModel = new MainViewModel(settings, config, fileToOpen);
        var window = new MainWindow { DataContext = viewModel };
        window.Width = settings.WindowWidth;
        window.Height = settings.WindowHeight;
        window.Show();

        // 6. «Горячее» обновление: файл настроек изменили во время работы — применяем без перезапуска.
        //    Событие приходит из другого потока, поэтому переходим в поток интерфейса через Dispatcher
        ConfigManager.Changed += newConfig => Dispatcher.Invoke(() =>
        {
            ApplyAccentColor(newConfig.AccentColor);
            viewModel.ApplyConfig(newConfig);
        });

        // Ошибки, которые никто не поймал, пишем в журнал, чтобы программа не падала молча
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Необработанная ошибка");
            MessageBox.Show(args.Exception.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }

    /// <summary>Меняет основной цвет во всех окнах (кнопки, вкладки, выделение строк).</summary>
    public static void ApplyAccentColor(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        // Цвет при наведении — чуть темнее, светлый — почти белый с оттенком основного
        var hover = Color.FromRgb((byte)(color.R * 0.85), (byte)(color.G * 0.85), (byte)(color.B * 0.85));
        var light = Color.FromRgb((byte)(255 - (255 - color.R) * 0.1), (byte)(255 - (255 - color.G) * 0.1), (byte)(255 - (255 - color.B) * 0.1));

        Current.Resources["AccentBrush"] = new SolidColorBrush(color);
        Current.Resources["AccentHoverBrush"] = new SolidColorBrush(hover);
        Current.Resources["AccentLightBrush"] = new SolidColorBrush(light);
    }

    private void ShowConfigErrorAndExit(List<string> errors)
    {
        MessageBox.Show("Ошибка в настройках программы:\n\n" + string.Join("\n", errors) +
                        "\n\nИсправьте appsettings.json или удалите файл\n" + ConfigManager.UserFilePath,
                        "ResumeBuilder", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Программа закрыта");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
