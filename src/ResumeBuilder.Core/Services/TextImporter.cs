using ResumeBuilder.Core.Models;
using Serilog;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Заполнение контактов из обычного текста (например, из старого резюме в .txt)
/// с помощью регулярных выражений. Заполняются только пустые поля.
/// </summary>
public static class TextImporter
{
    /// <summary>Возвращает список того, что удалось найти и заполнить.</summary>
    public static List<string> FillContacts(Resume resume, string text)
    {
        ArgumentNullException.ThrowIfNull(resume);
        var found = new List<string>();
        PersonalInfo p = resume.Personal;

        List<string> emails = TextHelper.FindEmails(text);
        if (emails.Count > 0 && p.Email == "")
        {
            p.Email = emails[0];
            found.Add("E-mail: " + p.Email);
        }

        List<string> phones = TextHelper.FindPhones(text);
        if (phones.Count > 0 && p.Phone == "")
        {
            p.Phone = phones[0];
            found.Add("Телефон: " + p.Phone);
        }

        string? login = TextHelper.FindGitHubLogin(text);
        List<string> urls = TextHelper.FindUrls(text);
        if (string.IsNullOrEmpty(p.Website))
        {
            if (login != null)
                p.Website = "https://github.com/" + login;
            else if (urls.Count > 0)
                p.Website = urls[0];
            if (!string.IsNullOrEmpty(p.Website))
                found.Add("Сайт: " + p.Website);
        }

        DateTime? date = TextHelper.FindDate(text);
        if (date != null && p.BirthDate == null)
        {
            p.BirthDate = date;
            found.Add("Дата рождения: " + date.Value.ToString("dd.MM.yyyy"));
        }

        Log.ForContext(typeof(TextImporter)).Information("Из текста заполнено полей: {Count}", found.Count);
        return found;
    }
}
