using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using ResumeBuilder.Core.Validation;
using ResumeBuilder.Wpf.Services;
using Serilog;
using Res = ResumeBuilder.Wpf.Properties.Resources;

namespace ResumeBuilder.Wpf.ViewModels;

/// <summary>
/// Главная ViewModel окна. Хранит текущее резюме и команды меню.
/// Списки (опыт, образование, навыки) вынесены в отдельные ViewModel.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private static ILogger Logger => Log.ForContext(typeof(MainViewModel));

    private readonly AppSettings _settings;
    private Resume _resume = new();
    private string _status = "";
    private BitmapImage? _qrImage;
    private bool _includeQr = true;
    private bool _hideContacts;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;

        NewCommand = new RelayCommand(NewResume);
        OpenCommand = new RelayCommand(Open);
        SaveCommand = new RelayCommand(Save);
        SampleCommand = new RelayCommand(() => SetResume(Resume.CreateSample()));
        ExportPdfCommand = new RelayCommand(ExportPdf);
        ExportWordCommand = new RelayCommand(ExportWord);
        ExportExcelCommand = new RelayCommand(ExportExcel);
        ExportReportCommand = new RelayCommand(ExportReport);
        ImportWordCommand = new RelayCommand(ImportWord);
        ImportTextCommand = new RelayCommand(ImportText);
        ReportPreviewCommand = new RelayCommand(ShowReportPreview);
        CompareFormatsCommand = new RelayCommand(CompareFormats);
        PrintCommand = new RelayCommand(Print);
        ValidateCommand = new RelayCommand(() => Validate());
        NormalizePhoneCommand = new RelayCommand(NormalizePhone);
        UpdateQrCommand = new RelayCommand(UpdateQr);
        RussianCommand = new RelayCommand(() => ChangeLanguage("ru-RU"));
        EnglishCommand = new RelayCommand(() => ChangeLanguage("en-US"));

        // При запуске: последний файл → черновик из кэша → пример
        Resume? draft = DraftCache.TryLoad();
        if (settings.LastFile != null && File.Exists(settings.LastFile))
            OpenFile(settings.LastFile);
        else if (draft != null)
        {
            SetResume(draft);
            Status = Res.Status_DraftRestored;
        }
        else
            SetResume(Resume.CreateSample());
    }

    // ---------- данные для привязки ----------

    public PersonalInfo Personal => _resume.Personal;

    public string About
    {
        get => _resume.About;
        set { _resume.About = value; OnPropertyChanged(); }
    }

    public ListViewModel<WorkExperience> Experience { get; } = new();
    public ListViewModel<Education> Education { get; } = new();
    public ListViewModel<Skill> Skills { get; } = new();

    public ObservableCollection<string> Errors { get; } = new();

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public BitmapImage? QrImage
    {
        get => _qrImage;
        set { _qrImage = value; OnPropertyChanged(); }
    }

    public bool IncludeQr
    {
        get => _includeQr;
        set { _includeQr = value; OnPropertyChanged(); }
    }

    public bool HideContacts
    {
        get => _hideContacts;
        set { _hideContacts = value; OnPropertyChanged(); }
    }

    public string Title => Res.App_Title + " — " + (_resume.FilePath == null ? Res.App_NewFile : Path.GetFileName(_resume.FilePath));

    // ---------- команды ----------

    public RelayCommand NewCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SampleCommand { get; }
    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ExportWordCommand { get; }
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand ExportReportCommand { get; }
    public RelayCommand ImportWordCommand { get; }
    public RelayCommand ImportTextCommand { get; }
    public RelayCommand ReportPreviewCommand { get; }
    public RelayCommand CompareFormatsCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand NormalizePhoneCommand { get; }
    public RelayCommand UpdateQrCommand { get; }
    public RelayCommand RussianCommand { get; }
    public RelayCommand EnglishCommand { get; }

    // ---------- работа с файлами ----------

    private void NewResume()
    {
        SetResume(new Resume());
        _settings.LastFile = null;
    }

    private void Open()
    {
        var dialog = new OpenFileDialog { Filter = ResumeFile.DialogFilter };
        if (dialog.ShowDialog() == true)
            OpenFile(dialog.FileName);
    }

    private void OpenFile(string path)
    {
        try
        {
            LoggedOperation.Run("Открытие файла", () =>
            {
                Resume resume = ResumeFile.GetSerializer(path).Load(path);
                SetResume(resume);
            });
            _settings.LastFile = path;
            Status = Res.Status_Opened + " " + Path.GetFileName(path);
            AuditLog.Write("Открытие", Path.GetFileName(path), true);
        }
        catch (Exception ex)
        {
            AuditLog.Write("Открытие", Path.GetFileName(path), false);
            ShowError(ex);
        }
    }

    private void Save()
    {
        CopyListsToResume();
        var dialog = new SaveFileDialog { Filter = ResumeFile.DialogFilter, FileName = "resume" };
        if (dialog.ShowDialog() != true)
            return;

        string fileName = Path.GetFileName(dialog.FileName);
        try
        {
            LoggedOperation.Run("Сохранение файла", () => ResumeFile.GetSerializer(dialog.FileName).Save(_resume, dialog.FileName));
            _resume.FilePath = dialog.FileName;
            _settings.LastFile = dialog.FileName;
            OnPropertyChanged(nameof(Title));
            Status = Res.Status_Saved + " " + fileName;
            AuditLog.Write("Сохранение", fileName, true);
        }
        catch (Exception ex)
        {
            AuditLog.Write("Сохранение", fileName, false);
            ShowError(ex);
        }
    }

    // ---------- экспорт ----------

    private void ExportPdf()
    {
        CopyListsToResume();
        if (!Validate() && MessageBox.Show(Res.Msg_ExportWithErrors, Res.App_Title, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        string? path = AskSavePath("PDF (*.pdf)|*.pdf", "resume.pdf");
        if (path != null)
            RunExport("Экспорт в PDF", () => PdfExporter.Export(_resume, path, IncludeQr, HideContacts), path);
    }

    private void ExportWord()
    {
        CopyListsToResume();
        string? path = AskSavePath("Word (*.docx)|*.docx", "resume.docx");
        if (path != null)
            RunExport("Экспорт в Word", () => WordExporter.ExportResume(_resume, path), path);
    }

    private void ExportExcel()
    {
        CopyListsToResume();
        string? path = AskSavePath("Excel (*.xlsx)|*.xlsx", "resume.xlsx");
        if (path != null)
            RunExport("Экспорт в Excel", () => ExcelExporter.ExportResume(_resume, path), path);
    }

    /// <summary>Сводный отчёт: считаем один раз, сохраняем в Excel, Word и txt.</summary>
    private void ExportReport()
    {
        CopyListsToResume();
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            string folder = dialog.FolderName;
            LoggedOperation.Run("Сводный отчёт", () =>
            {
                // Данные считаются ОДИН раз, а выводятся в три формата
                ResumeReport report = ReportCalculator.Calculate(_resume, DateTime.Today);
                ExcelExporter.ExportReport(report, Path.Combine(folder, "report.xlsx"));
                WordExporter.ExportReport(report, Path.Combine(folder, "report.docx"));
                File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToText());
            });
            Status = Res.Status_ReportSaved + " " + folder;
            AuditLog.Write("Сводный отчёт", folder, true);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ImportWord()
    {
        var dialog = new OpenFileDialog { Filter = "Word (*.docx)|*.docx" };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var errors = new List<string>();
            List<WorkExperience> items = WordImporter.ImportExperience(dialog.FileName, errors);
            foreach (WorkExperience item in items)
                Experience.Items.Add(item);

            string message = string.Format(Res.Msg_Imported, items.Count);
            if (errors.Count > 0)
                message += "\n\n" + string.Join("\n", errors);
            MessageBox.Show(message, Res.App_Title);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    /// <summary>Заполнить пустые контакты из текстового файла (регулярные выражения).</summary>
    private void ImportText()
    {
        var dialog = new OpenFileDialog { Filter = "Текст (*.txt)|*.txt" };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            string text = File.ReadAllText(dialog.FileName);
            List<string> found = TextImporter.FillContacts(_resume, text);
            OnPropertyChanged(nameof(Personal));
            UpdateQr();
            string message = found.Count == 0 ? Res.Msg_NothingFound : string.Join("\n", found);
            MessageBox.Show(message, Res.App_Title);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    /// <summary>Окно с отчётом в виде текстовой таблицы (из него можно напечатать или сохранить txt).</summary>
    private void ShowReportPreview()
    {
        CopyListsToResume();
        ResumeReport report = ReportCalculator.Calculate(_resume, DateTime.Today);
        var window = new Views.ReportWindow { DataContext = new ReportViewModel(report.ToText()) };
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }

    /// <summary>Сравнение JSON и двоичного формата по размеру и скорости.</summary>
    private void CompareFormats()
    {
        CopyListsToResume();
        MessageBox.Show(SerializationComparison.Compare(_resume), Res.Menu_CompareFormats);
    }

    private void Print()
    {
        CopyListsToResume();
        try
        {
            if (PrintService.Print(_resume))
            {
                Status = Res.Status_Printed;
                AuditLog.Write("Печать", _resume.Personal.FullName, true);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    // ---------- проверка и прочее ----------

    /// <summary>Проверяет резюме и показывает ошибки справа. Возвращает true, если ошибок нет.</summary>
    private bool Validate()
    {
        CopyListsToResume();
        Errors.Clear();
        foreach (string error in ResumeValidator.Validate(_resume))
            Errors.Add(error);
        Status = Errors.Count == 0 ? Res.Status_NoErrors : string.Format(Res.Status_Errors, Errors.Count);
        return Errors.Count == 0;
    }

    private void NormalizePhone()
    {
        Personal.Phone = TextHelper.NormalizePhone(Personal.Phone);
        OnPropertyChanged(nameof(Personal));
    }

    private void UpdateQr()
    {
        try
        {
            byte[] png = QrCodeService.CreatePng(QrCodeService.BuildVCard(Personal));
            var image = new BitmapImage();
            using var stream = new MemoryStream(png);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            QrImage = image;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Не удалось создать QR-код");
        }
    }

    private void ChangeLanguage(string language)
    {
        _settings.Language = language;
        _settings.Save();
        MessageBox.Show(Res.Msg_RestartForLanguage, Res.App_Title);
    }

    /// <summary>Вызывается при закрытии окна.</summary>
    public void OnClosing(double width, double height)
    {
        // Кэш черновика: при следующем запуске резюме восстановится, даже если его не сохранили
        CopyListsToResume();
        DraftCache.Save(_resume);

        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        _settings.Save();
    }

    // ---------- вспомогательные методы ----------

    private void SetResume(Resume resume)
    {
        _resume = resume;
        Experience.Load(resume.Experience);
        Education.Load(resume.Education);
        Skills.Load(resume.Skills);
        Errors.Clear();
        OnPropertyChanged(nameof(Personal));
        OnPropertyChanged(nameof(About));
        OnPropertyChanged(nameof(Title));
        UpdateQr();
    }

    /// <summary>Переносит изменения из таблиц на экране обратно в объект резюме.</summary>
    private void CopyListsToResume()
    {
        _resume.Experience = Experience.Items.ToList();
        _resume.Education = Education.Items.ToList();
        _resume.Skills = Skills.Items.ToList();
    }

    private static string? AskSavePath(string filter, string fileName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void RunExport(string operationName, Action export, string path)
    {
        try
        {
            LoggedOperation.Run(operationName, export);
            Status = Res.Status_Exported + " " + Path.GetFileName(path);
            AuditLog.Write(operationName, Path.GetFileName(path), true);
            if (MessageBox.Show(Res.Msg_OpenFile, Res.App_Title, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AuditLog.Write(operationName, Path.GetFileName(path), false);
            ShowError(ex);
        }
    }

    private static void ShowError(Exception ex)
    {
        Logger.Error(ex, "Ошибка");
        MessageBox.Show(Res.Msg_Error + "\n" + ex.Message, Res.App_Title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
