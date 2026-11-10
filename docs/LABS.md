# Соответствие лабораторным работам и листу задания

Документ для защиты: где в коде реализовано каждое требование. Пути указаны от корня репозитория.

## Лаба 21 — Сериализация (двоичная и JSON)

| Требование | Реализация |
|---|---|
| Класс с атрибутом сериализации, исключение поля | `Core/Models/Resume.cs` — `FilePath` помечен `[JsonIgnore]` и не пишется в `.rbin` (аналог `[NonSerialized]`) |
| JSON-сериализация | `Infrastructure/Serialization/JsonResumeSerializer.cs` (System.Text.Json, кириллица без экранирования, enum строками) |
| Двоичная сериализация | `Infrastructure/Serialization/BinaryResumeSerializer.cs` — **собственный формат** на `BinaryWriter`: сигнатура `RSMB`, версия, защита от повреждённых файлов |
| Почему не BinaryFormatter (вариант 2 «Безопасность») | комментарий в `BinaryResumeSerializer.cs`: небезопасен, удалён в .NET 9 |
| Сравнение форматов (вариант 1) | `ResumeFileService.Benchmark()`, команда `resumebuilder bench-serialize` |
| Сценарий 1: сохранение состояния (окно, последний файл, язык) | `Infrastructure/Settings/AppSettingsStore.cs`, `Wpf/MainWindow.xaml.cs` (`OnClosing`, `RestoreWindowPlacement`) |
| Сценарий 2: двоичный кэш | автосохранение черновика в `autosave.rbin` раз в минуту и при закрытии — `MainViewModel.Autosave()` / `Initialize()` |
| Версионность | `Resume.SchemaVersion` + проверка в `JsonResumeSerializer`; `FormatVersion` в двоичном формате |
| Связь с Memento (вопрос 12) | `Core/History/ResumeHistory.cs` — снимок состояния = сериализация |
| Тесты | `tests/.../SerializationTests.cs` |

## Лаба 22 — Импорт-экспорт Excel и Word (с правками преподавателя)

| Требование | Реализация |
|---|---|
| **Модуль 1 (Excel):** запись сущности проекта в .xlsx | `Infrastructure/Office/ExcelExperienceExporter.cs` — сущность **«Место работы»** (+ лист «Навыки») |
| жирный заголовок, границы, закреплённая строка | `ApplyTableStyle()`: `Font.Bold`, `Border`, `SheetView.FreezeRows(1)`; + автофильтр, формат дат, итог формулой `SUM` |
| **Модуль 2 (Word):** абзац с описанием + таблица + изображение | `Infrastructure/Office/WordResumeExporter.cs` (раздел «О себе», таблица опыта, QR-код картинкой) на `WordDocumentWriter.cs` (Open XML SDK) |
| **Модуль 3 «Сводный отчёт»:** одни данные → Excel и Word без дублирования подсчёта | агрегация: `Core/Reports/ResumeStatisticsService.cs` → `ReportDocumentBuilder.cs` → формат-независимый `ReportDocument`; рендеринг: `Infrastructure/Reports/ReportRenderers.cs` (Excel, Word, PDF, TXT); оркестрация: `ReportService.ExportAll()` |
| **На оценку выше — «Обратная задача»** | `Infrastructure/Office/WordExperienceImporter.cs`: поиск таблицы по метке `tblCaption` или заголовкам, сопоставление столбцов по названию (ru/en), разбор дат RegEx, ошибки по строкам. Меню «Экспорт → Импорт опыта из Word», команда `import-docx` |
| Дополнительно — «Оптимизация» | `Infrastructure/Office/ExcelExportBenchmark.cs`, команда `bench-excel 20000`: 3 стратегии (по ячейкам / InsertData / потоковый OpenXmlWriter) с замером времени и памяти |
| Почему не Interop | README и комментарии: нужен установленный Excel, не потокобезопасен, не работает в Docker |
| Тесты | `ReportAndExportTests.cs` (Excel-форматирование, Word туда-обратно на ru и en) |

## Лаба 23 — Вывод данных на печать

| Этап | Реализация |
|---|---|
| 1. Ввод: загрузка из файла | `ResumeFileService.Load()` |
| 2. Вычисления: статистика | `ResumeStatisticsService` (стаж, средние, максимум, доли, заполненность) |
| 3. Форматирование в текстовую таблицу | `TextReportRenderer` (псевдографика) |
| 4. Экспорт: консоль, .txt, PDF, печать на принтер | команда `report` (консоль + txt/pdf/xlsx/docx); WPF «Файл → Печать»: `Wpf/Services/PrintService.cs` (`FlowDocument` + `PrintDialog`) |

## Лаба 24 — Журналирование

| Задание | Реализация |
|---|---|
| 1.1 уровни DEBUG/INFO/ERROR | везде через `ILogger<T>`; демонстрация — команда `log-demo` |
| 1.2 DEBUG/INFO в консоль, WARNING+ ещё и в файл | `appsettings.json` → `Serilog:WriteTo` (`restrictedToMinimumLevel: Warning` у файла) |
| 1.3 иерархия по пространствам имён | `Serilog:MinimumLevel:Override` |
| 2.1 конфигурация во внешнем файле | `appsettings.json` + `Infrastructure/Logging/LoggingSetup.cs` (`ReadFrom.Configuration`) |
| 2.2 структурированный JSON | приёмник `CompactJsonFormatter` → `app-*.json` (timestamp, level, message, ExecutionTimeMs, CorrelationId) |
| 2.3 ротация по размеру и дате, лимит архивов | `rollingInterval: Day`, `fileSizeLimitBytes: 10 МБ`, `retainedFileCountLimit` |
| 3.1 CorrelationId | `Core/Services/OperationTimer.cs` (scope → LogContext) |
| 3.2 аудит в отдельный файл со своей политикой хранения | `SerilogAuditLogger`, секция `AuditLog` (ротация раз в месяц, 24 файла); персональные данные не пишутся |
| 3.3 время операций, в основной журнал — только медленные | `OperationTimer`: быстрые → Debug, медленные (> порога из конфигурации) → Warning |

## Лаба 25 — Локализация и интернационализация

| Задание | Реализация |
|---|---|
| 1.1 вынести строки из кода | все строки интерфейса и сообщения — в `Core/Resources/Strings.resx` (ru) и `Strings.en.resx` (en); в коде только ключи |
| 2.1 ResourceManager + .resx, en-US и ru-RU | `Core/Localization/ILocalizer.cs` (`ResxLocalizer`); переключение языка без перезапуска — `Wpf/Services/LocalizationManager.cs`, `{loc:Tr Ключ}` в XAML |
| 3.1 даты, числа, валюты для разных локалей | `Core/Localization/CultureFormatter.cs`; сравнение ru-RU / en-US / de-DE — команда `locale` |
| Склонение числительных (1 год / 2 года / 5 лет) | `CultureFormatter.PluralForm()` |
| Интернационализация данных | язык документа отдельно от языка интерфейса (`Resume.CultureName`), порядок «Имя Фамилия» для английского резюме |
| Тесты | `LocalizationTests.cs` |

## Лаба 26 — Регулярные выражения и валидация (с правками преподавателя)

| Требование | Реализация |
|---|---|
| **RegEx модуль 2:** поиск и проверка формата на данных проекта | `Core/Text/RegexPatterns.cs` (e-mail, телефон, URL, ФИО, даты, периоды работы, ссылки, href); `Core/Text/TextExtractor.cs` — «умная вставка» |
| **RegEx модуль 3:** группы, замены, lookaround | `Core/Text/TextFormatter.cs`: нормализация телефона `+375 (${code}) ${p1}-${p2}-${p3}`, даты в ISO, маскирование e-mail через `MatchEvaluator`, «Фамилия Имя» → «Имя Фамилия», lookbehind для логина GitHub и навыков, негативный lookbehind для Telegram |
| RegEx модуль 4 (доп.) | разбор «Ключ: значение» (`KeyValueLine`), извлечение href из HTML |
| **Валидация модуль 1:** ручные проверки | `Core/Validation/Validators.cs` → `ManualResumeValidator` (+ `EnsureValid` с исключением) |
| **Валидация модуль 2:** DataAnnotations | атрибуты на `PersonalInfo`, `WorkExperience`, `Skill`...; `AnnotationResumeValidator` (стандартный `Validator.TryValidateObject`) |
| свои атрибуты (задание 2.3) | `Core/Validation/Attributes/FormatAttributes.cs`: `EmailFormat`, `PhoneFormat`, `UrlFormat`, `PersonName`, `NotInFuture`, `MinAge` |
| Валидация модуль 4.3 (доп.): отчёт по полям и серьёзности | `ValidationReport` (Error / Warning, `ByField()`), панель «Проверка» в окне |
| Тесты | `TextProcessingTests.cs`, `ValidationTests.cs` |

## Лист задания — что уже закрыто

| Пункт пояснительной записки | Статус |
|---|---|
| Git + GitHub, стратегия разработки | репозиторий + CI `.github/workflows/build.yml` |
| ООП: абстракция, инкапсуляция, наследование, полиморфизм | `ResumeItem` → `WorkExperience`, `Education`, `Skill`, `LanguageSkill`; `PdfResumeTemplate` → `Classic`/`Modern` |
| Паттерны проектирования | Builder (`ResumeFluentBuilder`), Memento (`ResumeHistory`), Template Method (`PdfResumeTemplate`), Strategy (`IResumeSerializer`, `IResumeExporter`, `IReportRenderer`, `IResumeValidator`), Composite (`CompositeResumeValidator`), Observer + Command (MVVM), Singleton (`LocalizationManager`), Factory (`ResumeFileService.GetSerializer`), DI |
| Средства ведения документации | XML-комментарии + генерация XML-файла (`Directory.Build.props`), README, этот документ |
| Ресурсы приложения | `.resx`, иконка `Wpf/Resources/app.ico` (Build Action = Resource), стили в `App.xaml` |
| Пользовательские библиотеки | `ResumeBuilder.Core` и `ResumeBuilder.Infrastructure` — отдельные DLL |
| Журналирование | лаба 24 |
| Двоичная сериализация / JSON | лаба 21 |
| Локализация | лаба 25 |
| Обработка текста, RegEx | лаба 26 |
| Unit-тестирование | `tests/ResumeBuilder.Tests` |
| Развёртывание, установочный пакет, Docker | **следующий этап** (лабы 28, 31–33) |
