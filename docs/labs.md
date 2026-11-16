# Где в проекте лабораторные работы

Нумерация — по итоговому плану (10 лабораторных, последняя из трёх частей).

| № | Лабораторная | Статус | Где в коде |
|---|---|---|---|
| 1 | Сериализация (lab21) | готово | Классы моделей помечены `[Serializable]`, поле пути к файлу — `[NonSerialized]` / `[JsonIgnore]` (`Models/Resume.cs`). JSON и двоичный формат — `Services/ResumeSerializers.cs`. Сравнение форматов (вариант 1) — `SerializationComparison`, меню «Файл → Сравнить JSON и двоичный формат». Сценарий 1 (настройки окна, язык, последний файл) — `AppSettings.cs`. Сценарий 2 (кэш черновика в двоичном файле) — `DraftCache` |
| 2 | RegEx и валидация (lab26) | готово | RegEx модуль 2: проверка формата e-mail/телефона, поиск e-mail, телефонов, ссылок, дат в тексте — `Services/TextHelper.cs`, применяется в «Экспорт → Заполнить контакты из текста» (`TextImporter.cs`). Модуль 3: группы и замены (`NormalizePhone`, `DatesToIso`, `MaskEmails`, `FindDate`), lookbehind (`FindGitHubLogin`). Валидация модуль 1: ручные проверки — `Validation/ResumeValidator.ValidateManual`, проверка параметров методов (`ArgumentNullException`). Модуль 2: атрибуты DataAnnotations на моделях + свой атрибут `BelarusPhoneAttribute` |
| 3 | Excel/Word импорт-экспорт (lab22) | готово | `ExcelExporter.cs` (4 листа: резюме, опыт, образование, навыки; жирная шапка, границы, закреплённая строка, формулы), `WordExporter.cs` (абзацы, таблицы, QR-картинка), сводный отчёт: подсчёт в `ResumeReport.cs` → Excel, Word, txt; обратная задача: `WordImporter.cs` |
| 4 | Печать (lab23) | готово | 1) ввод — резюме из файла; 2) вычисления — `ReportCalculator`; 3) текстовая таблица — `ResumeReport.ToText()`; 4) вывод: окно «Файл → Просмотр отчёта» (на экран), сохранение в .txt, печать на принтер (`Wpf/Services/PrintService.cs`), PDF (`PdfExporter.cs`) |
| 5 | Логирование (lab24) | готово | 1.1 уровни Debug/Information/Warning/Error; 1.2 Debug и Info — в консоль и окно «Вывод» VS, Warning+ — ещё и в `logs/log-*.txt`; 1.3 разные уровни для пространств имён — `Override` в `Wpf/appsettings.json`; 2.1 вся настройка в `appsettings.json`; 2.2 JSON-журнал `logs/log-*.json` с UserId и ExecutionTimeMs; 2.3 ротация по дням и 10 МБ, хранится 7 файлов; 3.1 CorrelationId — `Services/LogHelper.cs` (`LoggedOperation`); 3.2 аудит в `logs/audit-*.log`, хранится 12 месяцев (`AuditLog`); 3.3 время операций, медленные (> 1000 мс) — Warning и попадают в основной файл |
| 6 | Unit-тесты / TDD (lab28) | готово | Отдельный проект `tests/ResumeBuilder.Tests` (xUnit), 8 классов тестов. Перед/после каждого теста — конструктор и `Dispose()` (`FileTests`), на весь класс — `IClassFixture<TestFolderFixture>`. Исключения — `ExceptionTests`, время выполнения и таймаут — `PerformanceTests`. Пример TDD — проверка повторяющихся навыков (`ValidationTests` → `ResumeValidator`). Описание и тестовый план — `docs/testing.md` |
| 7 | Документация (lab27) | готово | 1.1 `README.md` (описание, зависимости, сборка, ссылка на лицензию); 1.2 `CHANGELOG.md` (релизы 1.0.0 и 1.1.0), `CONTRIBUTING.md`; 1.3 аудит — `docs/docs-audit.md`; 2.1 XML-комментарии с `param`/`returns`/`exception`/`example` — `TextHelper`, `ReportCalculator`, `ResumeValidator`; 2.2 XML-файл документации (`GenerateDocumentationFile` в `Core.csproj`) и HTML через DocFX — `docs/docfx`; 2.3 «было/стало» — `docs/docs-audit.md`; 3.1 `GLOSSARY.md`; 3.2 `docs/audiences.md` |
| 8 | Локализация (lab25) | готово | `Wpf/Properties/Resources.resx` + `.en.resx`, `Core/Resources/Messages.resx` + `.en.resx`, даты в PDF по культуре, переключение языка в меню |
| 9 | Конфигурирование (lab32) | — | |
| 10.1 | Сборка дистрибутива (lab31) | — | |
| 10.2 | Docker-образ (lab33, ч. 1) | — | |
| 10.3 | docker-compose / Kubernetes (lab33, ч. 2) и реинжиниринг | — | |

## ООП и MVVM

- Наследование и полиморфизм: `ResumeItem` → `WorkExperience`, `Education`, `Skill` (методы `GetDescription()`, `Validate()`).
- Интерфейс: `IResumeSerializer` → `JsonResumeSerializer`, `BinaryResumeSerializer`.
- MVVM: Model — `Core/Models`, View — `Wpf/Views/*.xaml` и `MainWindow.xaml`, ViewModel — `Wpf/ViewModels` (`MainViewModel`, `ListViewModel<T>`, `RelayCommand`).
