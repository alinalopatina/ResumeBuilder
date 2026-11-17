using System.ComponentModel.DataAnnotations;
using ResumeBuilder.Core.Resources;

namespace ResumeBuilder.Core.Configuration;

/// <summary>
/// Настройки программы (секция "App" в appsettings.json).
/// Значения, присвоенные здесь, — это уровень 1 иерархии: «значения по умолчанию в коде».
/// Их перекрывают файлы конфигурации, переменные окружения и аргументы командной строки.
/// </summary>
public class AppConfig
{
    /// <summary>Профиль (среда): Development — для разработки, Production — для пользователя.</summary>
    [RegularExpression("^(Development|Production)$",
        ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Config_Error_Environment))]
    public string Environment { get; set; } = "Production";

    /// <summary>Язык интерфейса, если пользователь ещё не выбирал его в меню.</summary>
    [RegularExpression("^(ru-RU|en-US)$",
        ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Config_Error_Language))]
    public string DefaultLanguage { get; set; } = "ru-RU";

    /// <summary>Папка, которая открывается в диалогах экспорта. Пусто — «Документы».</summary>
    public string ExportFolder { get; set; } = "";

    /// <summary>Размер одной точки QR-кода в пикселях.</summary>
    [Range(2, 20, ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Config_Error_QrSize))]
    public int QrPixelsPerModule { get; set; } = 8;

    /// <summary>Отмечена ли галочка «Добавить QR-код» при запуске.</summary>
    public bool IncludeQrByDefault { get; set; } = true;

    /// <summary>Как часто сохранять черновик, минут. 0 — только при закрытии.</summary>
    [Range(0, 60, ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Config_Error_Autosave))]
    public int AutosaveMinutes { get; set; } = 5;

    /// <summary>Основной цвет интерфейса.</summary>
    [RegularExpression("^#[0-9A-Fa-f]{6}$",
        ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Config_Error_Color))]
    public string AccentColor { get; set; } = "#4F46E5";

    /// <summary>Операции дольше этого порога (мс) пишутся в журнал как Warning.</summary>
    [Range(1, 60000, ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Config_Error_SlowMs))]
    public int SlowOperationMs { get; set; } = 1000;

    /// <summary>
    /// Проверка настроек: атрибуты (DataAnnotations) + ручная проверка папки.
    /// Пустой список — настройки правильные.
    /// </summary>
    public List<string> Validate()
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(this, new ValidationContext(this), results, validateAllProperties: true);
        var errors = results.Select(r => r.ErrorMessage ?? "").ToList();

        if (!string.IsNullOrWhiteSpace(ExportFolder) && !Directory.Exists(ExportFolder))
            errors.Add(string.Format(Messages.Config_Error_Folder, ExportFolder));
        return errors;
    }

    /// <summary>Папка для диалогов экспорта: из настроек или «Документы».</summary>
    public string GetExportFolder() =>
        !string.IsNullOrWhiteSpace(ExportFolder) && Directory.Exists(ExportFolder)
            ? ExportFolder
            : System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
}
