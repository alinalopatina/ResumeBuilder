using System.ComponentModel.DataAnnotations;
using ResumeBuilder.Core.Resources;
using ResumeBuilder.Core.Validation;

namespace ResumeBuilder.Core.Models;

/// <summary>
/// Личные данные. Атрибуты над свойствами — это валидация через DataAnnotations.
/// Тексты ошибок берутся из файла ресурсов Messages.resx (поэтому они переводятся).
/// </summary>
[Serializable]
public class PersonalInfo
{
    [Required(ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Error_FullNameRequired))]
    [StringLength(100, MinimumLength = 2, ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Error_FullNameLength))]
    public string FullName { get; set; } = "";

    [Required(ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Error_PositionRequired))]
    public string Position { get; set; } = "";

    [Required(ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Error_EmailRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Error_EmailFormat))]
    public string Email { get; set; } = "";

    // Свой атрибут валидации (см. Validation/BelarusPhoneAttribute.cs)
    [BelarusPhone]
    public string Phone { get; set; } = "";

    public string City { get; set; } = "";

    public DateTime? BirthDate { get; set; }

    /// <summary>Возраст в полных годах или null, если дата рождения не указана.</summary>
    public int? GetAge(DateTime today)
    {
        if (BirthDate == null)
            return null;
        int age = today.Year - BirthDate.Value.Year;
        if (BirthDate.Value.Date > today.AddYears(-age))
            age--;
        return age;
    }

    /// <summary>Ссылка на GitHub или портфолио — её можно закодировать в QR-код.</summary>
    [Url(ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Error_UrlFormat))]
    public string? Website { get; set; }
}
