using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Builder;
using ResumeBuilder.Core.History;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using ResumeBuilder.Core.Text;
using ResumeBuilder.Core.Validation;
using ResumeBuilder.Infrastructure.Office;
using ResumeBuilder.Infrastructure.Pdf;
using ResumeBuilder.Infrastructure.Reports;
using ResumeBuilder.Infrastructure.Serialization;
using ResumeBuilder.Infrastructure.Settings;
using ResumeBuilder.Wpf.Services;

namespace ResumeBuilder.Wpf.ViewModels;

/// <summary>Списки значений для выпадающих списков в XAML (<c>{x:Static vm:Lookups.SkillCategories}</c>).</summary>
public static class Lookups
{
    public static SkillCategory[] SkillCategories { get; } = Enum.GetValues<SkillCategory>();
    public static LanguageLevel[] LanguageLevels { get; } = Enum.GetValues<LanguageLevel>();
    public static ResumeTemplate[] Templates { get; } = Enum.GetValues<ResumeTemplate>();
    public static QrContentMode[] QrModes { get; } = Enum.GetValues<QrContentMode>();
    public static int[] SkillLevels { get; } = [1, 2, 3, 4, 5];
    public static string[] Currencies { get; } = ["BYN", "USD", "EUR", "RUB", "PLN"];
    public static string[] Cultures { get; } = ["ru-RU", "en-US"];
}

/// <summary>
/// Главная ViewModel. Связывает интерфейс с сервисами Core/Infrastructure;
/// сама ничего не сериализует и не рисует — только координирует (принцип единственной ответственности).
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly ResumeFileService _files;
    private readonly IResumeValidator _validator;
    private readonly TextExtractor _extractor;
    private readonly PdfResumeExporter _pdf;
    private readonly WordResumeExporter _word;
    private readonly ExcelExperienceExporter _excel;
    private readonly IExperienceImporter _importer;
    private readonly ReportService _reports;
    private readonly AppSettingsStore _settingsStore;
    private readonly BinaryResumeSerializer _binary;
    private readonly IQrCodeGenerator _qr;
    private readonly ILocalizer _loc;
    private readonly IDialogService _dialogs;
    private readonly PrintService _print;
    private readonly IAuditLogger _audit;
    private readonly ILogger<MainViewModel> _logger;
    private readonly ResumeHistory _history = new();

    private Resume _resume = new();
    private string _statusText = string.Empty;
    private string _smartPasteText = string.Empty;
    private string _regexPreview = string.Empty;
    private bool _maskContacts;
    private bool _includeQr = true;
    private BitmapImage? _qrPreview;
    private TextExtractionResult? _lastExtraction;

    public MainViewModel(
        ResumeFileService files, IResumeValidator validator, TextExtractor extractor,
        PdfResumeExporter pdf, WordResumeExporter word, ExcelExperienceExporter excel,
        IExperienceImporter importer, ReportService reports, AppSettingsStore settingsStore,
        BinaryResumeSerializer binary, IQrCodeGenerator qr, ILocalizer loc, IDialogService dialogs,
        PrintService print, IAuditLogger audit, ILogger<MainViewModel> logger)
    {
        (_files, _validator, _extractor, _pdf, _word, _excel, _importer, _reports, _settingsStore, _binary, _qr, _loc, _dialogs, _print, _audit, _logger) =
            (files, validator, extractor, pdf, word, excel, importer, reports, settingsStore, binary, qr, loc, dialogs, print, audit, logger);

        Settings = _settingsStore.Load();
        _maskContacts = Settings.MaskContactsByDefault;

        NewCommand = new RelayCommand(NewResume);
        SampleCommand = new RelayCommand(LoadSample);
        OpenCommand = new RelayCommand(Open);
        SaveCommand = new RelayCommand(() => Save(false));
        SaveAsCommand = new RelayCommand(() => Save(true));
        ExportPdfCommand = new RelayCommand(() => Export(_pdf));
        ExportWordCommand = new RelayCommand(() => Export(_word));
        ExportExcelCommand = new RelayCommand(() => Export(_excel));
        ExportReportCommand = new RelayCommand(ExportReport);
        ImportWordCommand = new RelayCommand(ImportFromWord);
        PrintCommand = new RelayCommand(Print);
        ValidateCommand = new RelayCommand(() => Validate(showMessage: true));
        UndoCommand = new RelayCommand(Undo, () => _history.CanUndo);
        AnalyzeTextCommand = new RelayCommand(AnalyzeText, () => !string.IsNullOrWhiteSpace(SmartPasteText));
        ApplyExtractionCommand = new RelayCommand(ApplyExtraction, () => _lastExtraction is { IsEmpty: false });
        RefreshQrCommand = new RelayCommand(RefreshQr);
        NormalizeCommand = new RelayCommand(NormalizeContacts);
        SetLanguageCommand = new RelayCommand(p => SetUiLanguage(p as string ?? "ru-RU"));

        _loc.CultureChanged += (_, _) => OnPropertyChanged(nameof(WindowTitle));
    }

    // ───────────────────────── Свойства для привязки ─────────────────────────

    /// <summary>Пользовательские настройки (сохраняются при закрытии).</summary>
    public UserSettings Settings { get; }

    /// <summary>Текущее резюме. Поля PersonalInfo привязываются напрямую: <c>Resume.Personal.FullName</c>.</summary>
    public Resume Resume
    {
        get => _resume;
        private set
        {
            _resume = value;
            Experience = new ObservableCollection<WorkExperience>(value.Experience);
            Education = new ObservableCollection<Education>(value.Education);
            Skills = new ObservableCollection<Skill>(value.Skills);
            Languages = new ObservableCollection<LanguageSkill>(value.Languages);
            OnPropertyChanged(string.Empty); // обновить все привязки
        }
    }

    public ObservableCollection<WorkExperience> Experience { get; private set; } = [];
    public ObservableCollection<Education> Education { get; private set; } = [];
    public ObservableCollection<Skill> Skills { get; private set; } = [];
    public ObservableCollection<LanguageSkill> Languages { get; private set; } = [];
    public ObservableCollection<ValidationIssue> ValidationIssues { get; } = [];
    public ObservableCollection<string> ExtractedItems { get; } = [];

    public string? Summary
    {
        get => _resume.Summary;
        set { _resume.Summary = value; OnPropertyChanged(); }
    }

    public ResumeTemplate Template
    {
        get => _resume.Template;
        set { _resume.Template = value; OnPropertyChanged(); }
    }

    public QrContentMode QrMode
    {
        get => _resume.QrMode;
        set { _resume.QrMode = value; OnPropertyChanged(); RefreshQr(); }
    }

    /// <summary>Язык документа (заголовки разделов в PDF/Word), может отличаться от языка интерфейса.</summary>
    public string DocumentCulture
    {
        get => _resume.CultureName;
        set { _resume.CultureName = value; OnPropertyChanged(); }
    }

    public bool MaskContacts
    {
        get => _maskContacts;
        set => SetField(ref _maskContacts, value);
    }

    public bool IncludeQr
    {
        get => _includeQr;
        set => SetField(ref _includeQr, value);
    }

    public BitmapImage? QrPreview
    {
        get => _qrPreview;
        private set => SetField(ref _qrPreview, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string SmartPasteText
    {
        get => _smartPasteText;
        set
        {
            if (SetField(ref _smartPasteText, value)) UpdateRegexPreview();
        }
    }

    /// <summary>Демонстрация замен регулярными выражениями (модуль 3) на введённом тексте.</summary>
    public string RegexPreview
    {
        get => _regexPreview;
        private set => SetField(ref _regexPreview, value);
    }

    public string WindowTitle =>
        $"{_loc["App_Title"]} — {(_resume.FilePath is null ? _loc["App_Untitled"] : Path.GetFileName(_resume.FilePath))}";

    // ───────────────────────── Команды ─────────────────────────

    public RelayCommand NewCommand { get; }
    public RelayCommand SampleCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ExportWordCommand { get; }
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand ExportReportCommand { get; }
    public RelayCommand ImportWordCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand AnalyzeTextCommand { get; }
    public RelayCommand ApplyExtractionCommand { get; }
    public RelayCommand RefreshQrCommand { get; }
    public RelayCommand NormalizeCommand { get; }
    public RelayCommand SetLanguageCommand { get; }

    // ───────────────────────── Жизненный цикл ─────────────────────────

    /// <summary>
    /// Запуск: открыть последний файл → иначе восстановить автосохранение (двоичный кэш) → иначе пример.
    /// </summary>
    public void Initialize()
    {
        try
        {
            if (Settings.LastOpenedFile is { } last && File.Exists(last))
            {
                Resume = _files.Load(last);
                Status("Status_Opened", Path.GetFileName(last));
            }
            else if (File.Exists(_settingsStore.AutosavePath))
            {
                using var fs = File.OpenRead(_settingsStore.AutosavePath);
                Resume = _binary.Deserialize(fs);
                Status("Status_AutosaveRestored");
            }
            else
            {
                Resume = SampleData.CreateSampleResume();
                Status("Status_SampleLoaded");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось восстановить предыдущее состояние");
            Resume = SampleData.CreateSampleResume();
        }
        RefreshQr();
        Validate(showMessage: false);
    }

    /// <summary>Автосохранение черновика в двоичный файл (вызывается таймером и при закрытии).</summary>
    public void Autosave()
    {
        try
        {
            SyncToModel();
            Directory.CreateDirectory(_settingsStore.DataFolder);
            using var fs = File.Create(_settingsStore.AutosavePath);
            _binary.Serialize(_resume, fs);
            _logger.LogDebug("Автосохранение: {Bytes} байт", fs.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Автосохранение не удалось");
        }
    }

    /// <summary>Сохранение настроек при закрытии окна.</summary>
    public void SaveSettings()
    {
        Settings.UiCulture = _loc.Culture.Name;
        Settings.MaskContactsByDefault = MaskContacts;
        _settingsStore.Save(Settings);
    }

    // ───────────────────────── Реализация команд ─────────────────────────

    private void NewResume()
    {
        _history.Push(SnapshotSource(), _loc["Undo_New"]);
        Resume = new Resume { CultureName = _loc.Culture.Name };
        Settings.LastOpenedFile = null;
        ValidationIssues.Clear();
        RefreshQr();
        Status("Status_New");
    }

    private void LoadSample()
    {
        _history.Push(SnapshotSource(), _loc["Undo_Sample"]);
        Resume = SampleData.CreateSampleResume();
        RefreshQr();
        Status("Status_SampleLoaded");
    }

    private void Open()
    {
        var path = _dialogs.OpenFile(_files.DialogFilter);
        if (path is null) return;
        Safe(() =>
        {
            Resume = _files.Load(path);
            _history.Clear();
            Settings.AddRecent(path);
            RefreshQr();
            Validate(showMessage: false);
            Status("Status_Opened", Path.GetFileName(path));
        });
    }

    private void Save(bool saveAs)
    {
        SyncToModel();
        var path = _resume.FilePath;
        if (saveAs || path is null)
        {
            path = _dialogs.SaveFile(_files.DialogFilter, SuggestFileName(".resume.json"));
            if (path is null) return;
        }

        Safe(() =>
        {
            // Нормализуем телефон перед сохранением (RegEx, модуль 3)
            _resume.Personal.Phone = string.IsNullOrWhiteSpace(_resume.Personal.Phone) ? null : TextFormatter.NormalizePhone(_resume.Personal.Phone);
            _files.Save(_resume, path);
            Settings.AddRecent(path);
            OnPropertyChanged(nameof(WindowTitle));
            Status("Status_Saved", Path.GetFileName(path));
        });
    }

    private void Export(IResumeExporter exporter)
    {
        SyncToModel();
        if (!Validate(showMessage: false) && !_dialogs.Confirm(_loc["Confirm_ExportWithErrors"])) return;

        var filter = $"{exporter.FormatName} (*{exporter.FileExtension})|*{exporter.FileExtension}";
        var path = _dialogs.SaveFile(filter, SuggestFileName(exporter.FileExtension), Settings.LastExportFolder);
        if (path is null) return;

        Safe(() =>
        {
            var options = new ExportOptions { IncludeQrCode = IncludeQr, MaskContacts = MaskContacts, CultureName = DocumentCulture };
            using (var fs = File.Create(path)) exporter.Export(_resume, fs, options);
            Settings.LastExportFolder = Path.GetDirectoryName(path);
            _audit.Log($"Export{exporter.FormatName}", Path.GetFileName(path), true);
            Status("Status_Exported", Path.GetFileName(path));
            if (_dialogs.Confirm(_loc["Confirm_OpenFile"])) _dialogs.OpenWithShell(path);
        });
    }

    private void ExportReport()
    {
        SyncToModel();
        var folder = _dialogs.PickFolder(Settings.LastExportFolder);
        if (folder is null) return;
        Safe(() =>
        {
            var culture = CultureInfo.GetCultureInfo(DocumentCulture);
            var files = _reports.ExportAll(_resume, culture, folder, $"report-{SafeName()}");
            Settings.LastExportFolder = folder;
            Status("Status_ReportSaved", files.Count, folder);
            if (_dialogs.Confirm(_loc["Confirm_OpenFolder"])) _dialogs.OpenWithShell(folder);
        });
    }

    private void ImportFromWord()
    {
        var path = _dialogs.OpenFile("Word (*.docx)|*.docx", Settings.LastExportFolder);
        if (path is null) return;
        Safe(() =>
        {
            ImportResult<WorkExperience> result;
            using (var fs = File.OpenRead(path)) result = _importer.Import(fs);

            if (result.Items.Count > 0)
            {
                _history.Push(SnapshotSource(), _loc["Undo_Import"]);
                foreach (var item in result.Items)
                {
                    // Не добавляем дубликаты (та же компания, должность и дата начала)
                    if (Experience.Any(e => e.Company == item.Company && e.Position == item.Position && e.StartDate == item.StartDate)) continue;
                    Experience.Add(item);
                }
            }

            var sb = new StringBuilder(_loc.Format("Import_Result", result.Items.Count, result.Errors.Count));
            foreach (var error in result.Errors) sb.AppendLine().Append("• ").Append(error);
            _dialogs.Info(sb.ToString());
            _audit.Log("ImportWord", Path.GetFileName(path), result.Errors.Count == 0);
        });
    }

    private void Print()
    {
        SyncToModel();
        Safe(() =>
        {
            if (_print.Print(_resume))
            {
                _audit.Log("Print", SafeName(), true);
                Status("Status_Printed");
            }
        });
    }

    /// <summary>Запускает валидацию и обновляет список нарушений. Возвращает <c>true</c>, если ошибок нет.</summary>
    private bool Validate(bool showMessage)
    {
        SyncToModel();
        var report = _validator.Validate(_resume);
        ValidationIssues.Clear();
        foreach (var issue in report.Issues.OrderBy(i => i.Severity)) ValidationIssues.Add(issue);

        Status(report.IsPerfect ? "Status_ValidOk" : "Status_ValidIssues", report.Errors.Count(), report.Warnings.Count());
        if (showMessage && report.IsPerfect) _dialogs.Info(_loc["Status_ValidOk"]);
        return !report.HasErrors;
    }

    private void Undo()
    {
        var previous = _history.Undo();
        if (previous is null) return;
        previous.FilePath = _resume.FilePath;
        Resume = previous;
        RefreshQr();
        Status("Status_Undone");
    }

    private void AnalyzeText()
    {
        _lastExtraction = _extractor.Extract(SmartPasteText);
        ExtractedItems.Clear();
        foreach (var line in _lastExtraction.Describe()) ExtractedItems.Add(line);
        if (_lastExtraction.IsEmpty) ExtractedItems.Add(_loc["SmartPaste_NothingFound"]);
        Status("Status_Analyzed", ExtractedItems.Count);
    }

    private void ApplyExtraction()
    {
        if (_lastExtraction is null) return;
        _history.Push(SnapshotSource(), _loc["Undo_SmartPaste"]);
        SyncToModel();
        var changes = _extractor.ApplyTo(_lastExtraction, _resume);
        Resume = _resume; // перечитать коллекции и поля
        RefreshQr();
        Status("Status_Applied", changes);
    }

    private void NormalizeContacts()
    {
        _history.Push(SnapshotSource(), _loc["Undo_Normalize"]);
        var p = _resume.Personal;
        p.FullName = TextFormatter.CollapseSpaces(p.FullName);
        p.Email = p.Email.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(p.Phone)) p.Phone = TextFormatter.NormalizePhone(p.Phone);
        if (!string.IsNullOrWhiteSpace(p.Website)) p.Website = TextFormatter.NormalizeUrl(p.Website);
        Summary = TextFormatter.CollapseSpaces(Summary);
        OnPropertyChanged(nameof(Resume));
        RefreshQr();
        Status("Status_Normalized");
    }

    private void UpdateRegexPreview()
    {
        if (string.IsNullOrWhiteSpace(SmartPasteText))
        {
            RegexPreview = string.Empty;
            return;
        }
        var text = SmartPasteText;
        var sb = new StringBuilder();
        sb.AppendLine($"── {_loc["Regex_PhonesNormalized"]} ──").AppendLine(TextFormatter.NormalizePhonesInText(text));
        sb.AppendLine($"── {_loc["Regex_DatesIso"]} ──").AppendLine(TextFormatter.DatesToIso(text));
        sb.AppendLine($"── {_loc["Regex_Masked"]} ──").AppendLine(TextFormatter.MaskPhones(TextFormatter.MaskEmails(TextFormatter.NormalizePhonesInText(text))));
        RegexPreview = sb.ToString();
    }

    private void RefreshQr()
    {
        try
        {
            var payload = QrPayloadBuilder.Build(_resume);
            var png = _qr.GeneratePng(payload, 6);
            var image = new BitmapImage();
            using var ms = new MemoryStream(png);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            QrPreview = image;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось построить QR-код");
            QrPreview = null;
        }
    }

    private void SetUiLanguage(string cultureName)
    {
        LocalizationManager.Instance.SetCulture(CultureInfo.GetCultureInfo(cultureName));
        Validate(showMessage: false); // сообщения валидации — на новом языке
        UpdateRegexPreview();
        _logger.LogInformation("Язык интерфейса изменён на {Culture}", cultureName);
    }

    // ───────────────────────── Вспомогательное ─────────────────────────

    /// <summary>Переносит изменения из коллекций интерфейса обратно в модель.</summary>
    private void SyncToModel()
    {
        _resume.Experience = Experience.ToList();
        _resume.Education = Education.ToList();
        _resume.Skills = Skills.ToList();
        _resume.Languages = Languages.ToList();
    }

    private Resume SnapshotSource()
    {
        SyncToModel();
        return _resume;
    }

    private string SafeName()
    {
        var name = string.IsNullOrWhiteSpace(_resume.Personal.FullName) ? "resume" : _resume.Personal.FullName;
        return string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
    }

    private string SuggestFileName(string extension) => SafeName() + extension;

    private void Status(string key, params object?[] args) =>
        StatusText = args.Length == 0 ? _loc[key] : _loc.Format(key, args);

    private void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении команды");
            _dialogs.Error(_loc.Format("Error_Generic", ex.Message));
        }
    }
}
