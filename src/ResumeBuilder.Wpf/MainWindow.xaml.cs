using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using ResumeBuilder.Wpf.ViewModels;

namespace ResumeBuilder.Wpf;

/// <summary>
/// Главное окно. Code-behind минимален: вся логика — в <see cref="MainViewModel"/> (MVVM).
/// Здесь только то, что относится к самому окну: размер/положение и автосохранение по таймеру.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromMinutes(1) };

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        RestoreWindowPlacement();
        _vm.Initialize();

        _autosaveTimer.Tick += (_, _) => _vm.Autosave();
        _autosaveTimer.Start();
    }

    /// <summary>Сценарий «Сохранение состояния»: размер и положение окна восстанавливаются при запуске.</summary>
    private void RestoreWindowPlacement()
    {
        var s = _vm.Settings;
        Width = Math.Max(MinWidth, s.WindowWidth);
        Height = Math.Max(MinHeight, s.WindowHeight);
        if (s.WindowLeft is { } left && s.WindowTop is { } top &&
            left >= SystemParameters.VirtualScreenLeft && top >= SystemParameters.VirtualScreenTop &&
            left < SystemParameters.VirtualScreenWidth - 100 && top < SystemParameters.VirtualScreenHeight - 100)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        if (s.IsMaximized) WindowState = WindowState.Maximized;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var s = _vm.Settings;
        s.IsMaximized = WindowState == WindowState.Maximized;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        s.WindowLeft = bounds.Left;
        s.WindowTop = bounds.Top;
        s.WindowWidth = bounds.Width;
        s.WindowHeight = bounds.Height;

        _autosaveTimer.Stop();
        _vm.Autosave();
        _vm.SaveSettings();
        base.OnClosing(e);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}
