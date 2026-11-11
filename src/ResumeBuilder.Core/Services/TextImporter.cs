using ResumeBuilder.Core.Models;
using Serilog;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Заполнение контактов из обычного текста (например, из старого резюме в .txt)
/// с помощью регулярных выражений. Заполняются только пустые поля.
/// </summary>
public static class TextImporter
{
    /// <summary>
    /// Заполняет пустые поля. В found — что заполнено, в skipped — что найдено,
    /// но не записано, потому что поле уже было заполнено.
    /// </summary>
    public static List<string> FillContacts(Resume resume, string text) => FillContacts(resume, text, new List<string>());

    public static List<string> FillContacts(Resume resume, string text, List<string> skipped)
    {
        ArgumentNullException.ThrowIfNull(resume);
        var found = new List<string>();
        PersonalInfo p = resume.Personal;

        // Для каждого поля: нашли в тексте → если поле пустое, заполняем;
        // если уже заполнено — не трогаем, но сообщаем пользователю
        List<string> emails = TextHelper.FindEmails(text);
        if (emails.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(p.Email))
            {
                p.Email = emails[0];
                found.Add("E-mail: " + p.Email);
            }
            else
                skipped.Add("E-mail: " + emails[0]);
        }

        List<string> phones = TextHelper.FindPhones(text);
        if (phones.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(p.Phone))
            {
                p.Phone = phones[0];
                found.Add("Телефон: " + p.Phone);
            }
            else
                skipped.Add("Телефон: " + phones[0]);
        }

        // Сначала ищем GitHub (lookbehind), если его нет — любую другую ссылку
        string? website = null;
        string? login = TextHelper.FindGitHubLogin(text);
        if (login != null)
            website = "https://github.com/" + login;
        else
        {
            List<string> urls = TextHelper.FindUrls(text);
            if (urls.Count > 0)
                website = urls[0];
        }
        if (website != null)
        {
            if (string.IsNullOrWhiteSpace(p.Website))
            {
                p.Website = website;
                found.Add("Сайт: " + website);
            }
            else
                skipped.Add("Сайт: " + website);
        }

        DateTime? date = TextHelper.FindDate(text);
        if (date != null)
        {
            if (p.BirthDate == null)
            {
                p.BirthDate = date;
                found.Add("Дата рождения: " + date.Value.ToString("dd.MM.yyyy"));
            }
            else
                skipped.Add("Дата рождения: " + date.Value.ToString("dd.MM.yyyy"));
        }

        Log.ForContext(typeof(TextImporter)).Information("Из текста заполнено полей: {Count}", found.Count);
        return found;
    }
}
