using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using ResumeBuilder.Core.Configuration;
using Res = ResumeBuilder.Wpf.Properties.Resources;

namespace ResumeBuilder.Wpf.ViewModels;

/// <summary>
/// Окно «Настройки»: редактирование личных настроек, проверка, импорт и экспорт, сброс.
/// Работает с копией текущих настроек — пока не нажата «Сохранить», ничего не меняется.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private AppConfig _config;

    public SettingsViewModel()
    {
        _config = Copy(ConfigManager.Current);
        Sources = ConfigManager.DescribeSources();

        BrowseCommand = new RelayCommand(Browse);
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => CloseWindow?.Invoke());
        ImportCommand = new RelayCommand(Import);
        ExportCommand = new RelayCommand(Export);
        ResetCommand = new RelayCommand(Reset);
    }

    /// <summary>Как закрыть окно (задаётся при создании окна).</summary>
    public Action? CloseWindow { get; set; }

    // ---------- поля окна ----------

    public string[] Environments { get; } = { "Production", "Development" };
    public string[] Languages { get; } = { "ru-RU", "en-US" };

    public string Environment
    {
        get => _config.Environment;
        set { _config.Environment = value; OnPropertyChanged(); }
    }

    public string Language
    {
        get => _config.DefaultLanguage;
        set { _config.DefaultLanguage = value; OnPropertyChanged(); }
    }

    public string ExportFolder
    {
        get => _config.ExportFolder;
        set { _config.ExportFolder = value; OnPropertyChanged(); }
    }

    public int QrPixelsPerModule
    {
        get => _config.QrPixelsPerModule;
        set { _config.QrPixelsPerModule = value; OnPropertyChanged(); }
    }

    public bool IncludeQrByDefault
    {
        get => _config.IncludeQrByDefault;
        set { _config.IncludeQrByDefault = value; OnPropertyChanged(); }
    }

    public int AutosaveMinutes
    {
        get => _config.AutosaveMinutes;
        set { _config.AutosaveMinutes = value; OnPropertyChanged(); }
    }

    public string AccentColor
    {
        get => _config.AccentColor;
        set { _config.AccentColor = value; OnPropertyChanged(); OnPropertyChanged(nameof(AccentPreview)); }
    }

    /// <summary>Квадратик с выбранным цветом рядом с полем.</summary>
    public Brush AccentPreview
    {
        get
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(AccentColor)); }
            catch { return Brushes.Transparent; }
        }
    }

    /// <summary>Список источников настроек — видно, откуда что берётся.</summary>
    public string Sources { get; }

    public ObservableCollection<string> Errors { get; } = new();

    public RelayCommand BrowseCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ResetCommand { get; }

    // ---------- действия ----------

    private void Browse()
    {
        var dialog = new OpenFolderDialog { InitialDirectory = _config.GetExportFolder() };
        if (dialog.ShowDialog() == true)
            ExportFolder = dialog.FolderName;
    }

    /// <summary>Проверить и сохранить в usersettings.json. Программа сама заметит изменение файла.</summary>
    private void Save()
    {
        if (!IsValid())
            return;
        try
        {
            ConfigManager.SaveUserSettings(_config);
            ConfigManager.Reload();
            MessageBox.Show(Res.Msg_SettingsSaved, Res.App_Title);
            CloseWindow?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Res.Msg_Error + "\n" + ex.Message, Res.App_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Экспорт всех текущих настроек в файл — например, чтобы перенести на другой компьютер.</summary>
    private void Export()
    {
        if (!IsValid())
            return;
        var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "resumebuilder-settings.json" };
        if (dialog.ShowDialog() == true)
        {
            ConfigManager.SaveToFile(_config, dialog.FileName);
            MessageBox.Show(Res.Msg_Saved, Res.App_Title);
        }
    }

    /// <summary>Импорт настроек из файла: значения появляются в окне, применяются после «Сохранить».</summary>
    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            _config = ConfigManager.LoadFromFile(dialog.FileName, _config);
            RefreshAll();
            if (IsValid())
                MessageBox.Show(Res.Msg_SettingsImported, Res.App_Title);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Res.Msg_Error + "\n" + ex.Message, Res.App_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Удалить личные настройки — вернуться к настройкам из дистрибутива.</summary>
    private void Reset()
    {
        ConfigManager.ResetUserSettings();
        _config = Copy(ConfigManager.Bind(ConfigManager.Root));
        RefreshAll();
        Errors.Clear();
        MessageBox.Show(Res.Msg_SettingsReset, Res.App_Title);
    }

    private bool IsValid()
    {
        Errors.Clear();
        foreach (string error in _config.Validate())
            Errors.Add(error);
        return Errors.Count == 0;
    }

    private void RefreshAll()
    {
        // Пустое имя — «изменились все свойства», окно перечитает все поля
        OnPropertyChanged(string.Empty);
    }

    private static AppConfig Copy(AppConfig config) =>
        JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(config))!;
}
