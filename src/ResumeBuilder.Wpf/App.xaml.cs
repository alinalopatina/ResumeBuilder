using System.Globalization;
using System.Windows;
using System.Windows.Markup;
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
        LoggingSetup.Init();

        // Язык интерфейса берём из сохранённых настроек
        AppSettings settings = AppSettings.Load();
        var culture = new CultureInfo(settings.Language);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        ResumeBuilder.Wpf.Properties.Resources.Culture = culture;
        Messages.Culture = culture;

        // Чтобы даты в DatePicker отображались в формате выбранного языка
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        var window = new MainWindow { DataContext = new MainViewModel(settings) };
        window.Width = settings.WindowWidth;
        window.Height = settings.WindowHeight;
        window.Show();

        // Ошибки, которые никто не поймал, пишем в журнал, чтобы программа не падала молча
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Необработанная ошибка");
            MessageBox.Show(args.Exception.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Программа закрыта");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
