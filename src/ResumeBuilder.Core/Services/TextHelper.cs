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

    // Ссылка: http(s)://..., www.... или github.com/...
    private static readonly Regex UrlRegex = new(@"https?://[^\s,;]+|www\.[^\s,;]+|github\.com/[^\s,;]+", RegexOptions.IgnoreCase);

    // Lookbehind (?<=...): берём логин, который стоит ПОСЛЕ «github.com/», сам адрес в результат не попадает
    private static readonly Regex GitHubLoginRegex = new(@"(?<=github\.com/)[A-Za-z0-9-]+", RegexOptions.IgnoreCase);

    // Дата ДД.ММ.ГГГГ с группами day, month, year
    private static readonly Regex DateRegex = new(@"\b(?<day>\d{2})\.(?<month>\d{2})\.(?<year>\d{4})\b");

    /// <summary>Проверяет, что строка целиком является адресом e-mail.</summary>
    /// <param name="email">Проверяемая строка. Пробелы по краям не учитываются.</param>
    /// <returns><c>true</c>, если формат верный; <c>false</c> для пустой строки, <c>null</c> или неверного формата.</returns>
    /// <example>
    /// <code>
    /// TextHelper.IsValidEmail("maria@gmail.com"); // true
    /// TextHelper.IsValidEmail("wrong@mail");      // false — нет домена первого уровня
    /// </code>
    /// </example>
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email.Trim());

    /// <summary>Проверяет, что строка — белорусский номер телефона в одном из привычных видов.</summary>
    /// <param name="phone">Номер, например <c>+375 29 123-45-67</c> или <c>8 (029) 1234567</c>.</param>
    /// <returns><c>true</c>, если номер подходит под шаблон; иначе <c>false</c>.</returns>
    public static bool IsValidPhone(string? phone) =>
        !string.IsNullOrWhiteSpace(phone) && PhoneRegex.IsMatch(phone.Trim());

    /// <summary>Приводит номер телефона к единому виду <c>+375 (29) 123-45-67</c>.</summary>
    /// <param name="phone">Номер в любом допустимом виде.</param>
    /// <returns>
    /// Номер в едином виде. Если строка не похожа на белорусский номер,
    /// она возвращается без изменений (только без пробелов по краям).
    /// </returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="phone"/> равен <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// TextHelper.NormalizePhone("80291234567"); // "+375 (29) 123-45-67"
    /// TextHelper.NormalizePhone("123");         // "123"
    /// </code>
    /// </example>
    public static string NormalizePhone(string phone)
    {
        ArgumentNullException.ThrowIfNull(phone);
        string trimmed = phone.Trim();
        if (!PhoneRegex.IsMatch(trimmed))
            return trimmed;
        return PhoneRegex.Replace(trimmed, "+375 (${code}) ${p1}-${p2}-${p3}");
    }

    /// <summary>Находит все адреса e-mail в тексте.</summary>
    /// <param name="text">Произвольный текст, например старое резюме.</param>
    /// <returns>Список найденных адресов без повторов (может быть пустым).</returns>
    /// <exception cref="ArgumentNullException">Если <paramref name="text"/> равен <c>null</c>.</exception>
    public static List<string> FindEmails(string text) =>
        EmailSearchRegex.Matches(text).Select(m => m.Value).Distinct().ToList();

    /// <summary>Находит все телефоны в тексте и приводит их к одному виду.</summary>
    /// <param name="text">Произвольный текст.</param>
    /// <returns>Номера в виде <c>+375 (29) 123-45-67</c> без повторов.</returns>
    public static List<string> FindPhones(string text) =>
        PhoneSearchRegex.Matches(text).Select(m => NormalizePhone(m.Value)).Distinct().ToList();

    /// <summary>Находит все ссылки в тексте.</summary>
    /// <param name="text">Произвольный текст.</param>
    /// <returns>Ссылки без повторов. К ссылкам вида <c>www.site.by</c> добавляется <c>https://</c>.</returns>
    public static List<string> FindUrls(string text) =>
        UrlRegex.Matches(text).Select(m => m.Value.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? m.Value : "https://" + m.Value)
            .Distinct().ToList();

    /// <summary>Ищет логин GitHub с помощью lookbehind <c>(?&lt;=github\.com/)</c>.</summary>
    /// <param name="text">Текст, в котором может быть ссылка на GitHub.</param>
    /// <returns>Логин (без адреса сайта) или <c>null</c>, если ссылки нет.</returns>
    /// <example><c>FindGitHubLogin("мой код: github.com/maria-ivanova")</c> вернёт <c>"maria-ivanova"</c>.</example>
    public static string? FindGitHubLogin(string text)
    {
        Match match = GitHubLoginRegex.Match(text);
        return match.Success ? match.Value : null;
    }

    /// <summary>Ищет первую существующую дату вида ДД.ММ.ГГГГ. Дата собирается из групп day, month, year.</summary>
    /// <param name="text">Текст с датой.</param>
    /// <returns>Найденная дата или <c>null</c>. Несуществующие даты (например, 31.02.2005) пропускаются.</returns>
    public static DateTime? FindDate(string text)
    {
        foreach (Match m in DateRegex.Matches(text))
        {
            int day = int.Parse(m.Groups["day"].Value);
            int month = int.Parse(m.Groups["month"].Value);
            int year = int.Parse(m.Groups["year"].Value);
            // RegEx проверяет только формат, а 31.02 — несуществующая дата, её пропускаем
            if (year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
                return new DateTime(year, month, day);
        }
        return null;
    }

    /// <summary>Меняет все даты ДД.ММ.ГГГГ на формат ISO ГГГГ-ММ-ДД (перестановка групп в замене).</summary>
    /// <param name="text">Исходный текст.</param>
    /// <returns>Новый текст; исходная строка не меняется.</returns>
    public static string DatesToIso(string text) =>
        DateRegex.Replace(text, "${year}-${month}-${day}");

    /// <summary>Скрывает имя в адресах e-mail (для «публичной» версии резюме).</summary>
    /// <param name="text">Текст с адресами.</param>
    /// <returns>Текст, в котором <c>maria.ivanova@gmail.com</c> заменено на <c>ma***@gmail.com</c>.</returns>
    public static string MaskEmails(string text) =>
        EmailSearchRegex.Replace(text, m =>
        {
            string user = m.Groups["user"].Value;
            string start = user.Length > 2 ? user.Substring(0, 2) : user.Substring(0, 1);
            return start + "***@" + m.Groups["domain"].Value;
        });
}
