using System.ComponentModel.DataAnnotations;
using ResumeBuilder.Core.Validation.Attributes;

namespace ResumeBuilder.Core.Models;

/// <summary>
/// Личные данные кандидата.
/// </summary>
/// <remarks>
/// Свойства размечены атрибутами DataAnnotations — декларативный подход к валидации
/// (лаба «Валидация», модуль 2). Текст <c>ErrorMessage</c> — это ключ ресурса,
/// который <see cref="Validation.AnnotationResumeValidator"/> переводит на язык интерфейса.
/// </remarks>
public class PersonalInfo
{
    /// <summary>ФИО.</summary>
    [Required(ErrorMessage = "Validation_FullNameRequired")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Validation_FullNameLength")]
    [PersonName(ErrorMessage = "Validation_FullNameFormat")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Желаемая должность (заголовок резюме).</summary>
    [Required(ErrorMessage = "Validation_DesiredPositionRequired")]
    [StringLength(100, ErrorMessage = "Validation_TooLong")]
    public string DesiredPosition { get; set; } = string.Empty;

    /// <summary>E-mail.</summary>
    [Required(ErrorMessage = "Validation_EmailRequired")]
    [EmailFormat(ErrorMessage = "Validation_EmailFormat")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Телефон (любой разумный формат, при сохранении нормализуется).</summary>
    [PhoneFormat(ErrorMessage = "Validation_PhoneFormat")]
    public string? Phone { get; set; }

    /// <summary>Город проживания.</summary>
    public string? City { get; set; }

    /// <summary>Дата рождения.</summary>
    [NotInFuture(ErrorMessage = "Validation_BirthDateFuture")]
    [MinAge(14, ErrorMessage = "Validation_MinAge")]
    public DateTime? BirthDate { get; set; }

    /// <summary>Ссылка на сайт, GitHub, LinkedIn или портфолио (кодируется в QR).</summary>
    [UrlFormat(ErrorMessage = "Validation_UrlFormat")]
    public string? Website { get; set; }

    /// <summary>Желаемая зарплата (форматируется по правилам культуры).</summary>
    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "Validation_SalaryRange")]
    public decimal? DesiredSalary { get; set; }

    /// <summary>Код валюты ISO 4217 (BYN, USD, EUR...).</summary>
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Validation_CurrencyFormat")]
    public string SalaryCurrency { get; set; } = "BYN";

    /// <summary>Путь к фотографии (необязательно).</summary>
    public string? PhotoPath { get; set; }
}
