using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Core.Abstractions;
using ResumeBuilder.Core.Builder;
using ResumeBuilder.Core.Localization;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Services;
using ResumeBuilder.Core.Text;
using ResumeBuilder.Core.Validation;
using ResumeBuilder.Infrastructure.Office;
using ResumeBuilder.Infrastructure.Reports;
using ResumeBuilder.Infrastructure.Serialization;

namespace ResumeBuilder.Cli;

/// <summary>
/// Команды консольной утилиты. Каждая команда демонстрирует одну или несколько лабораторных.
/// </summary>
public sealed class CliCommands(
    ResumeFileService files,
    IEnumerable<IResumeExporter> exporters,
    ManualResumeValidator manualValidator,
    AnnotationResumeValidator annotationValidator,
    IResumeValidator validator,
    TextExtractor extractor,
    ReportService reports,
    IExperienceImporter importer,
    ILocalizer loc,
    IAuditLogger audit,
    ILogger<CliCommands> logger)
{
    private const string DefaultInput = "samples/sample.resume.json";

    /// <summary>Разбирает аргументы и запускает команду. Возвращает код выхода.</summary>
    public int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h") return Help();

        var opts = CliOptions.Parse(args.Skip(1).ToArray());
        if (opts.Get("lang") is { } lang) loc.Culture = CultureInfo.GetCultureInfo(lang);
        logger.LogDebug("Команда {Command}, аргументы {Args}", args[0], string.Join(' ', args.Skip(1)));

        return args[0] switch
        {
            "sample" => Sample(opts),
            "export" => Export(opts),
            "validate" => Validate(opts),
            "report" => Report(opts),
            "import-docx" => ImportDocx(opts),
            "regex" => RegexDemo(opts),
            "locale" => LocaleDemo(),
            "log-demo" => LogDemo(),
            "bench-serialize" => BenchSerialize(opts),
            "bench-excel" => BenchExcel(opts),
            _ => Unknown(args[0])
        };
    }

    // ───────── help ─────────
    private static int Help()
    {
        Console.WriteLine("""
            ResumeBuilder CLI — конструктор резюме с экспортом в PDF и QR-кодом

            Использование: resumebuilder <команда> [аргументы] [--lang ru-RU|en-US]

              sample [файл.resume.json]                 создать пример резюме (лаба «Сериализация»)
              export <вход> <выход.pdf|docx|xlsx|rbin|resume.json> [--mask] [--no-qr] [--template Classic|Modern]
                                                        экспорт/конвертация (PDF+QR, Word, Excel, двоичный, JSON)
              validate <вход>                           проверка: ручная + DataAnnotations (лаба «Валидация»)
              report <вход> [папка]                     сводный отчёт: консоль + txt/pdf/xlsx/docx (лабы «Печать», «Excel/Word»)
              import-docx <файл.docx>                   прочитать таблицу опыта из Word («обратная задача»)
              regex [текстовый файл]                    поиск/извлечение/замены регулярными выражениями
              locale                                    форматы дат, чисел, валют для ru-RU / en-US / de-DE
              log-demo                                  уровни журнала, CorrelationId, медленная операция
              bench-serialize [вход]                    сравнение JSON и двоичного формата (размер, скорость)
              bench-excel [строк=20000]                 замер экспорта большого набора в Excel (оптимизация)
            """);
        return 0;
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Неизвестная команда «{cmd}». Список команд: resumebuilder help");
        return 2;
    }

    // ───────── sample ─────────
    private int Sample(CliOptions o)
    {
        var path = o.Positional(0) ?? "sample.resume.json";
        var resume = SampleData.CreateSampleResume();
        EnsureFolder(path);
        files.Save(resume, path);
        Console.WriteLine($"✔ Пример резюме сохранён: {Path.GetFullPath(path)}");
        return 0;
    }

    // ───────── export ─────────
    private int Export(CliOptions o)
    {
        var input = o.Positional(0) ?? DefaultInput;
        var output = o.Positional(1) ?? throw new ArgumentException("Укажите выходной файл, например out/cv.pdf");
        var resume = files.Load(input);
        if (o.Get("template") is { } t) resume.Template = Enum.Parse<ResumeTemplate>(t, ignoreCase: true);
        if (o.Has("website-qr")) resume.QrMode = QrContentMode.Website;

        EnsureFolder(output);

        // Сериализаторы (.resume.json / .rbin) — это «конвертация» формата хранения
        if (output.EndsWith(".rbin", StringComparison.OrdinalIgnoreCase) || output.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            files.Save(resume, output);
            Console.WriteLine($"✔ Сохранено: {output} ({new FileInfo(output).Length:N0} байт)");
            return 0;
        }

        var exporter = exporters.FirstOrDefault(e => output.EndsWith(e.FileExtension, StringComparison.OrdinalIgnoreCase))
                       ?? throw new NotSupportedException($"Неизвестный формат: {Path.GetExtension(output)}");

        var report = validator.Validate(resume);
        if (report.HasErrors)
        {
            Console.WriteLine("⚠ В резюме есть ошибки (экспорт продолжается):");
            foreach (var e in report.Errors) Console.WriteLine($"   • {e.Message}");
        }

        var options = new ExportOptions
        {
            IncludeQrCode = !o.Has("no-qr"),
            MaskContacts = o.Has("mask"),
            CultureName = o.Get("lang") ?? resume.CultureName
        };

        var sw = Stopwatch.StartNew();
        using (var fs = File.Create(output)) exporter.Export(resume, fs, options);
        audit.Log($"Export{exporter.FormatName}", Path.GetFileName(output), true);
        Console.WriteLine($"✔ {exporter.FormatName}: {Path.GetFullPath(output)} ({new FileInfo(output).Length / 1024.0:0.0} КБ, {sw.ElapsedMilliseconds} мс)");
        return 0;
    }

    // ───────── validate ─────────
    private int Validate(CliOptions o)
    {
        var resume = files.Load(o.Positional(0) ?? DefaultInput);

        Console.WriteLine("── Модуль 1: ручные проверки ──");
        Print(manualValidator.Validate(resume));
        Console.WriteLine();
        Console.WriteLine("── Модуль 2: DataAnnotations ──");
        Print(annotationValidator.Validate(resume));
        Console.WriteLine();
        Console.WriteLine("── Модуль 4.3: сводный отчёт по полям ──");
        var all = validator.Validate(resume);
        foreach (var (field, issues) in all.ByField())
            Console.WriteLine($"  {field}: {string.Join("; ", issues.Select(i => $"{i.Severity}: {i.Message}"))}");
        if (all.IsPerfect) Console.WriteLine("  ✔ Нарушений нет");

        // Задание 1.1 — вариант с исключением
        try
        {
            manualValidator.EnsureValid(resume);
            Console.WriteLine("\nEnsureValid: ошибок нет, исключение не выброшено.");
        }
        catch (ResumeValidationException ex)
        {
            Console.WriteLine($"\nEnsureValid выбросил {nameof(ResumeValidationException)}:\n{ex.Message}");
        }
        return all.HasErrors ? 3 : 0;

        static void Print(ValidationReport r)
        {
            if (r.IsPerfect) { Console.WriteLine("  ✔ Нарушений нет"); return; }
            foreach (var i in r.Issues) Console.WriteLine($"  {(i.Severity == ValidationSeverity.Error ? "✖" : "⚠")} {i.Field}: {i.Message}");
        }
    }

    // ───────── report ─────────
    private int Report(CliOptions o)
    {
        var resume = files.Load(o.Positional(0) ?? DefaultInput);
        var folder = o.Positional(1) ?? "output";
        var culture = loc.Culture;

        // 1. Красивый вывод в консоль
        var document = reports.BuildReport(resume, culture);
        Console.WriteLine(new TextReportRenderer().RenderToString(document));

        // 2. Сохранение во все форматы (txt, pdf, xlsx, docx) — подсчёт выполняется один раз внутри
        var saved = reports.ExportAll(resume, culture, folder);
        Console.WriteLine("Файлы отчёта:");
        foreach (var f in saved) Console.WriteLine($"  • {Path.GetFullPath(f)}");
        return 0;
    }

    // ───────── import-docx ─────────
    private int ImportDocx(CliOptions o)
    {
        var path = o.Positional(0) ?? throw new ArgumentException("Укажите .docx-файл");
        using var fs = File.OpenRead(path);
        var result = importer.Import(fs);

        Console.WriteLine($"Прочитано записей: {result.Items.Count}");
        foreach (var e in result.Items) Console.WriteLine($"  ✔ {e.GetSummary(loc.Culture)}");
        foreach (var err in result.Errors) Console.WriteLine($"  ✖ {err}");
        return result.Errors.Count == 0 ? 0 : 4;
    }

    // ───────── regex ─────────
    private int RegexDemo(CliOptions o)
    {
        var text = o.Positional(0) is { } path ? File.ReadAllText(path) : DemoText;
        Console.WriteLine("── Исходный текст ──");
        Console.WriteLine(text);

        Console.WriteLine("── Модуль 2: поиск и извлечение ──");
        var found = extractor.Extract(text);
        foreach (var line in found.Describe()) Console.WriteLine($"  • {line}");
        Console.WriteLine($"  • href из HTML: {string.Join(", ", TextExtractor.ExtractHrefs("<p><a href=\"https://bntu.by\">БНТУ</a> <a class='x' href='https://github.com'>GH</a></p>"))}");

        Console.WriteLine("\n── Модуль 2: проверка формата ──");
        foreach (var email in new[] { "alina.lopatina@gmail.com", "wrong@mail", "user@site.by" })
            Console.WriteLine($"  e-mail {email,-28} → {(RegexPatterns.EmailFormat().IsMatch(email) ? "OK" : "неверно")}");
        foreach (var phone in new[] { "+375 (29) 123-45-67", "8 029 1234567", "12-34" })
            Console.WriteLine($"  телефон {phone,-27} → {(RegexPatterns.BelarusPhone().IsMatch(phone) ? "OK" : "неверно")}");

        Console.WriteLine("\n── Модуль 3: группы и замены ──");
        Console.WriteLine($"  Телефоны к единому виду : {TextFormatter.NormalizePhonesInText("звоните 80291234567 или +375-33-765-43-21")}");
        Console.WriteLine($"  Даты ДД.ММ.ГГГГ → ISO   : {TextFormatter.DatesToIso("с 01.09.2024 по 30.06.2028")}");
        Console.WriteLine($"  Маскирование e-mail     : {TextFormatter.MaskEmails("пишите на alina.lopatina@gmail.com")}");
        Console.WriteLine($"  Маскирование телефона   : {TextFormatter.MaskPhones("+375 (29) 123-45-67")}");
        Console.WriteLine($"  Фамилия Имя → Имя Фам.  : {TextFormatter.SwapLastFirstName("Лопатина Алина Николаевна")}");
        Console.WriteLine($"  Инициалы                : {TextFormatter.ToShortName("Лопатина Алина Николаевна")}");
        Console.WriteLine($"  Lookbehind (GitHub)     : {RegexPatterns.GitHubLogin().Match("мой профиль: github.com/alinalopatina").Value}");
        Console.WriteLine($"  Нормализация пробелов   : «{TextFormatter.CollapseSpaces("  Много   пробелов ,  и   знаков !  ")}»");
        return 0;
    }

    private const string DemoText = """
        ФИО: Петров   Иван Сергеевич
        Город: Минск
        Должность: QA Engineer
        Телефон: 8 (029) 765-43-21, e-mail: ivan.petrov@mail.ru
        Telegram: @ivan_petrov_qa
        Профиль: https://github.com/ivan-petrov
        Дата рождения: 12.05.2003
        Опыт: 09.2022 – 06.2024 — тестировщик в «СофтЛаб»; 07.2024 - н.в. — QA Engineer в «ТестКо»
        Навыки: Selenium, Postman, SQL, Jira; Git
        """;

    // ───────── locale ─────────
    private int LocaleDemo()
    {
        var resume = SampleData.CreateSampleResume();
        var job = resume.Experience[0];
        var cultures = new[] { "ru-RU", "en-US", "de-DE" }.Select(n => CultureInfo.GetCultureInfo(n)).ToArray();
        var date = new DateTime(2026, 12, 8, 14, 30, 0);

        Console.WriteLine("── Задание 2.1: строки из .resx по локали ──");
        foreach (var c in cultures.Take(2)) Console.WriteLine($"  {c.Name}: {loc.Get("App_Greeting", c)}");

        Console.WriteLine("\n── Задание 3.1: культурно-зависимое форматирование ──");
        Console.WriteLine($"  {"",-24}{string.Join("", cultures.Select(c => c.Name.PadRight(26)))}");
        Row("Дата (d)", c => date.ToString("d", c));
        Row("Дата и время (g)", c => date.ToString("g", c));
        Row("Месяц и год", c => date.ToString("MMMM yyyy", c));
        Row("Число 1234567.891", c => 1234567.891.ToString("N2", c));
        Row("Зарплата 2500 BYN", c => CultureFormatter.FormatMoney(2500m, "BYN", c));
        Row("Сумма 1234.5 EUR", c => CultureFormatter.FormatMoney(1234.5m, "EUR", c));
        Row("Доля 0.256", c => 0.256.ToString("P1", c));
        Row("Период работы", c => CultureFormatter.FormatPeriod(job.StartDate, job.EndDate, loc, c));
        Row("Стаж 1, 2, 5, 21 мес.", c => string.Join(", ", new[] { 1, 2, 5, 21 }.Select(m => CultureFormatter.FormatDuration(m, loc, c))));
        Row("Возраст 21, 22, 25", c => string.Join(", ", new[] { 21, 22, 25 }.Select(y => $"{y} {CultureFormatter.Plural(y, "Plural_Year", loc, c)}")));
        return 0;

        void Row(string title, Func<CultureInfo, string> format) =>
            Console.WriteLine($"  {title,-24}{string.Join("", cultures.Select(c => format(c).PadRight(26)))}");
    }

    // ───────── log-demo ─────────
    private int LogDemo()
    {
        Console.WriteLine("Записи уровня Debug видны в logs/cli-*.json; Warning+ — ещё и в logs/cli-*.log\n");
        logger.LogTrace("TRACE: самая подробная информация");
        logger.LogDebug("DEBUG: значение переменной {Value}", 42);
        logger.LogInformation("INFO: пользователь {User} открыл программу", Environment.UserName);
        logger.LogWarning("WARN: резюме без телефона — QR-код будет неполным");
        logger.LogError(new InvalidOperationException("Тестовая ошибка"), "ERROR: не удалось экспортировать файл {File}", "cv.pdf");

        // Корреляция: все записи внутри операции получают один CorrelationId
        using (var op = OperationTimer.Start(logger, "DemoRequest"))
        {
            logger.LogInformation("Шаг 1 запроса (CorrelationId={Id})", op.CorrelationId);
            reports.BuildReport(SampleData.CreateSampleResume(), loc.Culture); // логи сервиса — с тем же Id
            logger.LogInformation("Шаг 2 запроса");
        }

        // Медленная операция → Warning с ExecutionTimeMs
        using (OperationTimer.Start(logger, "SlowDemo"))
            Thread.Sleep(OperationTimer.SlowThresholdMs + 200);

        audit.Log("LogDemo", null, true);
        Console.WriteLine("\nГотово. Аудит записан в logs/audit-*.log");
        return 0;
    }

    // ───────── bench-serialize ─────────
    private int BenchSerialize(CliOptions o)
    {
        var resume = o.Positional(0) is { } p ? files.Load(p) : SampleData.CreateSampleResume();
        Console.WriteLine($"{"Формат",-18}{"Размер, байт",14}{"Запись, мкс",14}{"Чтение, мкс",14}");
        foreach (var r in files.Benchmark(resume, 2000))
            Console.WriteLine($"{r.Format,-18}{r.SizeBytes,14:N0}{r.SerializeMsPerOp * 1000,14:0.0}{r.DeserializeMsPerOp * 1000,14:0.0}");
        return 0;
    }

    // ───────── bench-excel ─────────
    private int BenchExcel(CliOptions o)
    {
        var rows = int.TryParse(o.Positional(0), out var n) ? n : 20_000;
        Console.WriteLine($"Генерация {rows:N0} строк...");
        var data = SampleData.GenerateExperience(rows);
        Console.WriteLine($"{"Стратегия",-50}{"мс",8}{"МБ выдел.",11}{"файл, КБ",10}");
        foreach (var r in ExcelExportBenchmark.Run(data, "output/bench"))
            Console.WriteLine($"{r.Strategy,-50}{r.ElapsedMs,8:N0}{r.AllocatedMb,11:N0}{r.FileKb,10:N0}");
        return 0;
    }

    private static void EnsureFolder(string filePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }
}

/// <summary>Простейший разбор аргументов: позиционные + <c>--ключ значение</c> + флаги <c>--флаг</c>.</summary>
public sealed class CliOptions
{
    private readonly List<string> _positional = [];
    private readonly Dictionary<string, string?> _named = new(StringComparer.OrdinalIgnoreCase);

    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--");
                // флаги без значения
                if (key is "mask" or "no-qr" or "website-qr") hasValue = false;
                o._named[key] = hasValue ? args[++i] : null;
            }
            else
            {
                o._positional.Add(args[i]);
            }
        }
        return o;
    }

    public string? Positional(int index) => index < _positional.Count ? _positional[index] : null;
    public string? Get(string key) => _named.TryGetValue(key, out var v) ? v : null;
    public bool Has(string key) => _named.ContainsKey(key);
}
