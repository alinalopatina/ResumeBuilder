using System.Text.RegularExpressions;

namespace ResumeBuilder.Core.Text;

/// <summary>
/// Преобразование текста с помощью групп захвата и подстановок
/// (лаба «Регулярные выражения», модуль 3). Все методы чистые (без побочных эффектов),
/// поэтому легко покрываются unit-тестами.
/// </summary>
public static class TextFormatter
{
    /// <summary>
    /// Приводит белорусский номер к единому виду <c>+375 (29) 123-45-67</c>.
    /// Если номер не белорусский — возвращает его без лишних символов (только цифры и «+»).
    /// </summary>
    /// <example><c>NormalizePhone("8 029 1234567")</c> → <c>"+375 (29) 123-45-67"</c></example>
    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var trimmed = phone.Trim();

        var match = RegexPatterns.BelarusPhone().Match(trimmed);
        if (match.Success && match.Length == trimmed.Length)
            return RegexPatterns.BelarusPhone().Replace(trimmed, "+375 (${code}) ${p1}-${p2}-${p3}");

        return RegexPatterns.NonPhoneChars().Replace(trimmed, string.Empty);
    }

    /// <summary>Заменяет в тексте все найденные белорусские номера на единый формат.</summary>
    public static string NormalizePhonesInText(string text) =>
        RegexPatterns.BelarusPhone().Replace(text, "+375 (${code}) ${p1}-${p2}-${p3}");

    /// <summary>
    /// Маскирует e-mail для «публичной» версии резюме: <c>alina.lopatina@gmail.com</c> → <c>al***@g***.com</c>.
    /// Используется <see cref="MatchEvaluator"/> — замена вычисляется кодом по группам.
    /// </summary>
    public static string MaskEmails(string text) =>
        RegexPatterns.EmailSearch().Replace(text, m =>
        {
            var user = m.Groups["user"].Value;
            var domain = m.Groups["domain"].Value;
            var tld = m.Groups["tld"].Value;
            var visibleUser = user.Length <= 2 ? user[..1] : user[..2];
            return $"{visibleUser}***@{domain[..1]}***.{tld}";
        });

    /// <summary>Маскирует номер телефона, оставляя код оператора и две последние цифры: <c>+375 (29) ***-**-67</c>.</summary>
    public static string MaskPhones(string text) =>
        RegexPatterns.BelarusPhone().Replace(text, "+375 (${code}) ***-**-${p3}");

    /// <summary>Переводит все даты ДД.ММ.ГГГГ в ISO-формат ГГГГ-ММ-ДД (обратные ссылки на группы).</summary>
    /// <example><c>"с 01.09.2022"</c> → <c>"с 2022-09-01"</c></example>
    public static string DatesToIso(string text) =>
        RegexPatterns.DateDdMmYyyy().Replace(text, "${year}-${month}-${day}");

    /// <summary>Меняет порядок «Фамилия Имя [Отчество]» → «Имя Фамилия» (для англоязычного резюме).</summary>
    /// <example><c>"Лопатина Алина Николаевна"</c> → <c>"Алина Лопатина"</c></example>
    public static string SwapLastFirstName(string fullName)
    {
        var trimmed = CollapseSpaces(fullName);
        return RegexPatterns.FullNameParts().IsMatch(trimmed)
            ? RegexPatterns.FullNameParts().Replace(trimmed, "${first} ${last}")
            : trimmed;
    }

    /// <summary>Возвращает инициалы: «Лопатина Алина Николаевна» → «Лопатина А. Н.».</summary>
    public static string ToShortName(string fullName)
    {
        var m = RegexPatterns.FullNameParts().Match(CollapseSpaces(fullName));
        if (!m.Success) return fullName.Trim();
        var middle = m.Groups["middle"].Success ? $" {m.Groups["middle"].Value[0]}." : string.Empty;
        return $"{m.Groups["last"].Value} {m.Groups["first"].Value[0]}.{middle}";
    }

    /// <summary>Убирает повторяющиеся пробелы, пробелы перед знаками препинания и по краям.</summary>
    public static string CollapseSpaces(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var result = RegexPatterns.MultipleSpaces().Replace(text, " ");
        result = RegexPatterns.SpaceBeforePunctuation().Replace(result, string.Empty);
        return result.Trim();
    }

    /// <summary>Делает ссылку абсолютной: <c>github.com/user</c> → <c>https://github.com/user</c>.</summary>
    public static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var trimmed = url.Trim().TrimEnd('.', ',', ';', ')');
        return Regex.IsMatch(trimmed, "^https?://", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))
            ? trimmed
            : "https://" + trimmed;
    }

    /// <summary>Экранирует спецсимволы для формата vCard (RFC 6350, раздел 3.4).</summary>
    public static string EscapeVCard(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var escaped = RegexPatterns.VCardSpecialChars().Replace(value, @"\$0");
        return escaped.Replace("\r\n", "\\n").Replace("\n", "\\n");
    }
}
