using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace ResumeBuilder.Wpf.Services;

/// <summary>
/// Диалоги вынесены за интерфейс, чтобы ViewModel не зависела от окон WPF
/// и её можно было тестировать (подставив заглушку).
/// </summary>
public interface IDialogService
{
    string? OpenFile(string filter, string? initialFolder = null);
    string? SaveFile(string filter, string defaultFileName, string? initialFolder = null);
    string? PickFolder(string? initialFolder = null);
    void Info(string message);
    void Error(string message);
    bool Confirm(string message);
    void OpenWithShell(string path);
}

/// <summary>Реализация на стандартных диалогах Windows.</summary>
public sealed class DialogService : IDialogService
{
    private static string Title => LocalizationManager.Instance["App_Title"];

    public string? OpenFile(string filter, string? initialFolder = null)
    {
        var dlg = new OpenFileDialog { Filter = filter, InitialDirectory = initialFolder ?? string.Empty };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SaveFile(string filter, string defaultFileName, string? initialFolder = null)
    {
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            InitialDirectory = initialFolder ?? string.Empty,
            AddExtension = true
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickFolder(string? initialFolder = null)
    {
        var dlg = new OpenFolderDialog { InitialDirectory = initialFolder ?? string.Empty };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    public void Info(string message) =>
        MessageBox.Show(message, Title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string message) =>
        MessageBox.Show(message, Title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message) =>
        MessageBox.Show(message, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void OpenWithShell(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
