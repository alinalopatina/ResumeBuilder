using System.ComponentModel;
using System.Windows;
using ResumeBuilder.Wpf.ViewModels;

namespace ResumeBuilder.Wpf;

/// <summary>Главное окно. Вся логика — в MainViewModel, здесь только закрытие окна.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OnClosing(Width, Height);
        base.OnClosing(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
