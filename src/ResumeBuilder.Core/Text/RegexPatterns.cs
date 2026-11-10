using System.Text.RegularExpressions;

namespace ResumeBuilder.Core.Text;

/// <summary>
/// Единый каталог регулярных выражений приложения.
/// </summary>
/// <remarks>
/// <para>Все выражения собраны в одном месте («единый источник истины»), чтобы их было
/// легко тестировать и документировать.</para>
/// <para>Используется генератор исходного кода <c>[GeneratedRegex]</c> (.NET 7+):
/// регулярное выражение компилируется в C#-код на этапе сборки, а не во время работы,
/// что быстрее, чем <c>RegexOptions.Compiled</c>. У каждого выражения задан таймаут —
/// защита от «катастрофического возврата» (ReDoS).</para>
/// </remarks>
public static partial class RegexPatterns
{
    private const int TimeoutMs = 1000;

    // ───────────────────────── Модуль 2: проверка формата (якоря ^ $) ─────────────────────────

    /// <summary>Проверка e-mail целиком: <c>user.name+tag@mail.example.by</c>.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$", RegexOptions.None, TimeoutMs)]
    public static partial Regex EmailFormat();

    /// <summary>
    /// Телефон «в любом разумном виде»: необязательный +, цифры, пробелы, дефисы, скобки.
    /// Количество цифр дополнительно проверяется в коде (7–15 по E.164).
    /// </summary>
    [GeneratedRegex(@"^\+?\d[\d\s\-()]{5,20}\d$", RegexOptions.None, TimeoutMs)]
    public static partial Regex PhoneFormat();

    /// <summary>URL: схема необязательна, домен из 2+ частей, произвольный путь.</summary>
    [GeneratedRegex(@"^(?:https?://)?(?:[\w-]+\.)+[a-z]{2,}(?:[/?#][^\s]*)?$", RegexOptions.IgnoreCase, TimeoutMs)]
    public static partial Regex UrlFormat();

    /// <summary>ФИО: от 1 до 4 слов из букв любого алфавита, допускаются дефис и апостроф (Анна-Мария, O'Brien).</summary>
    [GeneratedRegex(@"^\p{L}[\p{L}'\-]*(?:\s+\p{L}[\p{L}'\-]*){0,3}$", RegexOptions.None, TimeoutMs)]
    public static partial Regex PersonName();

    /// <summary>Код валюты ISO 4217.</summary>
    [GeneratedRegex(@"^[A-Z]{3}$", RegexOptions.None, TimeoutMs)]
    public static partial Regex CurrencyCode();

    // ───────────────────────── Модуль 2: поиск в тексте (без якорей) ─────────────────────────

    /// <summary>Поиск e-mail в тексте. Именованные группы <c>user</c>, <c>domain</c>, <c>tld</c> используются для маскирования.</summary>
    [GeneratedRegex(@"(?<user>[A-Za-z0-9._%+-]+)@(?<domain>[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*)\.(?<tld>[A-Za-z]{2,})", RegexOptions.None, TimeoutMs)]
    public static partial Regex EmailSearch();

    /// <summary>
    /// Поиск белорусского номера в любом написании: <c>+375 29 123-45-67</c>, <c>8 (029) 1234567</c>, <c>80291234567</c>.
    /// Группы: <c>code</c> — код оператора, <c>p1</c>, <c>p2</c>, <c>p3</c> — части номера.
    /// </summary>
    [GeneratedRegex(@"(?<![\d+])(?:\+?\s*375|8\s*\(?\s*0)[\s\-(]*(?<code>\d{2})[\s\-)]*(?<p1>\d{3})[\s\-]*(?<p2>\d{2})[\s\-]*(?<p3>\d{2})(?!\d)", RegexOptions.None, TimeoutMs)]
    public static partial Regex BelarusPhone();

    /// <summary>Поиск даты в формате ДД.ММ.ГГГГ с группами <c>day</c>, <c>month</c>, <c>year</c>.</summary>
    [GeneratedRegex(@"\b(?<day>0[1-9]|[12]\d|3[01])\.(?<month>0[1-9]|1[0-2])\.(?<year>(?:19|20)\d{2})\b", RegexOptions.None, TimeoutMs)]
    public static partial Regex DateDdMmYyyy();

    /// <summary>
    /// Период работы: <c>09.2021 – 06.2024</c>, <c>01.2023 - н.в.</c>, <c>03.2020 — present</c>.
    /// </summary>
    [GeneratedRegex(@"(?<from>(?:0[1-9]|1[0-2])\.(?:19|20)\d{2})\s*[-–—]\s*(?<to>(?:0[1-9]|1[0-2])\.(?:19|20)\d{2}|н\.?\s?в\.?|по\s+настоящее\s+время|present|now)", RegexOptions.IgnoreCase, TimeoutMs)]
    public static partial Regex WorkPeriod();

    /// <summary>Годы учёбы: <c>2022–2026</c>.</summary>
    [GeneratedRegex(@"\b(?<from>(?:19|20)\d{2})\s*[-–—]\s*(?<to>(?:19|20)\d{2})\b", RegexOptions.None, TimeoutMs)]
    public static partial Regex YearRange();

    /// <summary>Ссылки в тексте: http(s)://..., www...., github.com/..., linkedin.com/..., t.me/...</summary>
    [GeneratedRegex(@"\bhttps?://[^\s<>""']+|\bwww\.[^\s<>""']+|\b(?:github\.com|linkedin\.com|t\.me|gitlab\.com)/[^\s<>""']+", RegexOptions.IgnoreCase, TimeoutMs)]
    public static partial Regex UrlSearch();

    /// <summary>Атрибут href в HTML-фрагменте (задание 2.3 — извлечение ссылок из HTML).</summary>
    [GeneratedRegex(@"<a\s[^>]*?href\s*=\s*([""'])(?<url>.*?)\1", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeoutMs)]
    public static partial Regex HtmlHref();

    // ───────────────────────── Модуль 3: lookaround ─────────────────────────

    /// <summary>
    /// Логин GitHub <b>после</b> «github.com/» — позитивная ретроспективная проверка (lookbehind):
    /// сам префикс в результат не попадает.
    /// </summary>
    [GeneratedRegex(@"(?<=github\.com/)[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})", RegexOptions.IgnoreCase, TimeoutMs)]
    public static partial Regex GitHubLogin();

    /// <summary>
    /// Ник Telegram вида <c>@nickname</c>. Негативный lookbehind <c>(?&lt;![\w.])</c> исключает e-mail
    /// (у e-mail перед «@» стоит буква).
    /// </summary>
    [GeneratedRegex(@"(?<![\w.])@(?<handle>[A-Za-z][A-Za-z0-9_]{4,31})\b", RegexOptions.None, TimeoutMs)]
    public static partial Regex TelegramHandle();

    /// <summary>Всё, что написано после слова «Навыки:» / «Skills:» до конца строки (lookbehind переменной длины).</summary>
    [GeneratedRegex(@"(?<=(?:Навыки|Ключевые\s+навыки|Skills)\s*:\s*)\S[^\r\n]*", RegexOptions.IgnoreCase, TimeoutMs)]
    public static partial Regex SkillsLine();

    // ───────────────────────── Модуль 3: группы и замены ─────────────────────────

    /// <summary>«Фамилия Имя [Отчество]» с группами <c>last</c>, <c>first</c>, <c>middle</c>.</summary>
    [GeneratedRegex(@"^(?<last>\p{Lu}[\p{L}'\-]+)\s+(?<first>\p{Lu}[\p{L}'\-]+)(?:\s+(?<middle>\p{Lu}[\p{L}'\-]+))?$", RegexOptions.None, TimeoutMs)]
    public static partial Regex FullNameParts();

    /// <summary>Два и более пробельных символа подряд (для нормализации).</summary>
    [GeneratedRegex(@"[ \t ]{2,}", RegexOptions.None, TimeoutMs)]
    public static partial Regex MultipleSpaces();

    /// <summary>Пробел перед знаком препинания: «текст ,текст» → «текст, текст».</summary>
    [GeneratedRegex(@"\s+(?=[,.;:!?])", RegexOptions.None, TimeoutMs)]
    public static partial Regex SpaceBeforePunctuation();

    /// <summary>Символы, которые нужно экранировать в vCard: обратный слеш, запятая, точка с запятой.</summary>
    [GeneratedRegex(@"[\\,;]", RegexOptions.None, TimeoutMs)]
    public static partial Regex VCardSpecialChars();

    /// <summary>Любой символ, кроме цифр и «+».</summary>
    [GeneratedRegex(@"[^\d+]", RegexOptions.None, TimeoutMs)]
    public static partial Regex NonPhoneChars();

    // ───────────────────────── Модуль 4 (доп.): полуструктурированный текст ─────────────────────────

    /// <summary>Строка вида «Ключ: значение» (многострочный режим).</summary>
    [GeneratedRegex(@"^[ \t]*(?<key>[\p{L}][\p{L} \-]{1,30}?)[ \t]*:[ \t]*(?<value>[^\r\n]+?)[ \t]*\r?$", RegexOptions.Multiline, TimeoutMs)]
    public static partial Regex KeyValueLine();
}
