using System.IO;
using System.Windows;
using Microsoft.Win32;
using ResumeBuilder.Wpf.Services;
using Res = ResumeBuilder.Wpf.Properties.Resources;

namespace ResumeBuilder.Wpf.ViewModels;

/// <summary>Окно предпросмотра отчёта: текст отчёта, печать и сохранение в .txt.</summary>
public class ReportViewModel : ViewModelBase
{
    public ReportViewModel(string reportText)
    {
        ReportText = reportText;
        PrintCommand = new RelayCommand(Print);
        SaveTxtCommand = new RelayCommand(SaveTxt);
    }

    public string ReportText { get; }

    public RelayCommand PrintCommand { get; }
    public RelayCommand SaveTxtCommand { get; }

    private void Print()
    {
        PrintService.PrintText(ReportText, Res.Report_WindowTitle);
    }

    private void SaveTxt()
    {
        var dialog = new SaveFileDialog { Filter = "Текст (*.txt)|*.txt", FileName = "report.txt" };
        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, ReportText);
            MessageBox.Show(Res.Msg_Saved, Res.App_Title);
        }
    }
}
