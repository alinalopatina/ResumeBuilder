using System.Text.RegularExpressions;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Работа с текстом через регулярные выражения.
/// Проверка формата (e-mail, телефон), поиск в тексте, замены с группами.
/// </summary>
public static class TextHelper
{
    // ^ и $ — проверяем строку целиком
    private static readonly Regex EmailRegex =
        new(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$");

    // Без ^ и $ — ищем внутри текста. Именованные группы user и domain нужны для маскирования
    private static readonly Regex EmailSearchRegex =
        new(@"(?<user>[A-Za-z0-9._%+-]+)@(?<domain>[A-Za-z0-9.-]+\.[A-Za-z]{2,})");

    // Белорусский номер: +375 29 123-45-67, 8 (029) 1234567, 80291234567 ...
    // Группы: code — код оператора, p1, p2, p3 — части номера
    private const string PhonePattern =
        @"(?:\+?375|8\s*\(?0)\s*\(?(?<code>\d{2})\)?[\s-]*(?<p1>\d{3})[\s-]*(?<p2>\d{2})[\s-]*(?<p3>\d{2})";

    private static readonly Regex PhoneRegex = new("^" + PhonePattern + "$");
    private static readonly Regex PhoneSearchRegex = new(PhonePattern);

    // Дата ДД.ММ.ГГГГ с группами day, month, year
    private static readonly Regex DateRegex = new(@"\b(?<day>\d{2})\.(?<month>\d{2})\.(?<year>\d{4})\b");

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email.Trim());

    public static bool IsValidPhone(string? phone) =>
        !string.IsNullOrWhiteSpace(phone) && PhoneRegex.IsMatch(phone.Trim());

    /// <summary>Приводит номер к виду +375 (29) 123-45-67. Если номер не подходит — возвращает как есть.</summary>
    public static string NormalizePhone(string phone)
    {
        string trimmed = phone.Trim();
        if (!PhoneRegex.IsMatch(trimmed))
            return trimmed;
        return PhoneRegex.Replace(trimmed, "+375 (${code}) ${p1}-${p2}-${p3}");
    }

    /// <summary>Находит все e-mail в тексте.</summary>
    public static List<string> FindEmails(string text) =>
        EmailSearchRegex.Matches(text).Select(m => m.Value).Distinct().ToList();

    /// <summary>Находит все телефоны в тексте и приводит их к одному виду.</summary>
    public static List<string> FindPhones(string text) =>
        PhoneSearchRegex.Matches(text).Select(m => NormalizePhone(m.Value)).Distinct().ToList();

    /// <summary>Меняет даты ДД.ММ.ГГГГ на ГГГГ-ММ-ДД (перестановка групп).</summary>
    public static string DatesToIso(string text) =>
        DateRegex.Replace(text, "${year}-${month}-${day}");

    /// <summary>Скрывает часть e-mail: maria.ivanova@gmail.com → ma***@gmail.com</summary>
    public static string MaskEmails(string text) =>
        EmailSearchRegex.Replace(text, m =>
        {
            string user = m.Groups["user"].Value;
            string start = user.Length > 2 ? user.Substring(0, 2) : user.Substring(0, 1);
            return start + "***@" + m.Groups["domain"].Value;
        });
}
