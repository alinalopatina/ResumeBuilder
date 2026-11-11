# Где в проекте лабораторные работы

Нумерация — по итоговому плану (10 лабораторных, последняя из трёх частей).

| № | Лабораторная | Статус | Где в коде |
|---|---|---|---|
| 1 | Сериализация (lab21) | готово | `Core/Services/ResumeSerializers.cs` (JSON и двоичный формат), `Core/Services/AppSettings.cs` (настройки окна и языка между запусками), `Resume.FilePath` не сохраняется (`[JsonIgnore]`) |
| 2 | RegEx и валидация (lab26) | готово | `Core/Services/TextHelper.cs` (проверка e-mail и телефона, поиск в тексте, замены с группами), `Core/Validation/ResumeValidator.cs` (ручные проверки + DataAnnotations), свой атрибут `BelarusPhoneAttribute.cs` |
| 3 | Excel/Word импорт-экспорт (lab22) | готово | `ExcelExporter.cs` (4 листа: резюме, опыт, образование, навыки; жирная шапка, границы, закреплённая строка, формулы), `WordExporter.cs` (абзацы, таблицы, QR-картинка), сводный отчёт: подсчёт в `ResumeReport.cs` → Excel, Word, txt; обратная задача: `WordImporter.cs` |
| 4 | Печать (lab23) | готово | `Wpf/Services/PrintService.cs` (FlowDocument + PrintDialog), `ResumeReport.ToText()` → report.txt, PDF через `PdfExporter.cs` |
| 5 | Логирование (lab24) | готово | Serilog: `Core/Services/LoggingSetup.cs`, настройки в `Wpf/appsettings.json` |
| 6 | Unit-тесты / TDD (lab28) | частично | `tests/ResumeBuilder.Tests` — тесты есть, по лабе нужно дописать |
| 7 | Документация (lab27) | частично | README, этот файл, комментарии в коде — по лабе нужно дописать |
| 8 | Локализация (lab25) | готово | `Wpf/Properties/Resources.resx` + `.en.resx`, `Core/Resources/Messages.resx` + `.en.resx`, даты в PDF по культуре, переключение языка в меню |
| 9 | Конфигурирование (lab32) | — | |
| 10.1 | Сборка дистрибутива (lab31) | — | |
| 10.2 | Docker-образ (lab33, ч. 1) | — | |
| 10.3 | docker-compose / Kubernetes (lab33, ч. 2) и реинжиниринг | — | |

## ООП и MVVM

- Наследование и полиморфизм: `ResumeItem` → `WorkExperience`, `Education`, `Skill` (методы `GetDescription()`, `Validate()`).
- Интерфейс: `IResumeSerializer` → `JsonResumeSerializer`, `BinaryResumeSerializer`.
- MVVM: Model — `Core/Models`, View — `Wpf/Views/*.xaml` и `MainWindow.xaml`, ViewModel — `Wpf/ViewModels` (`MainViewModel`, `ListViewModel<T>`, `RelayCommand`).
