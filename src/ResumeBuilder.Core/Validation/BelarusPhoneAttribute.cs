using System.ComponentModel.DataAnnotations;
using ResumeBuilder.Core.Resources;
using ResumeBuilder.Core.Services;

namespace ResumeBuilder.Core.Validation;

/// <summary>Свой атрибут валидации: телефон в белорусском формате. Пустое значение допускается.</summary>
public class BelarusPhoneAttribute : ValidationAttribute
{
    public BelarusPhoneAttribute()
    {
        ErrorMessageResourceType = typeof(Messages);
        ErrorMessageResourceName = nameof(Messages.Error_PhoneFormat);
    }

    public override bool IsValid(object? value)
    {
        string? phone = value as string;
        return string.IsNullOrWhiteSpace(phone) || TextHelper.IsValidPhone(phone);
    }
}
