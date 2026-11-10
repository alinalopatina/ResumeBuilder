# Где в проекте лабораторные работы

| № | Лабораторная | Где в коде |
|---|---|---|
| 21 | Сериализация данных приложения | `Core/Services/ResumeSerializers.cs` (JSON и двоичный формат), `Core/Services/AppSettings.cs` (настройки окна и языка сохраняются между запусками), `Resume.FilePath` исключён из сохранения (`[JsonIgnore]`) |
| 22 | Импорт-экспорт данных в Excel и Word | `Core/Services/ExcelExporter.cs` (опыт работы: жирная шапка, границы, закреплённая строка), `WordExporter.cs` (абзац, таблица, картинка QR), сводный отчёт: подсчёт в `ResumeReport.cs` → вывод в Excel и Word; обратная задача: `WordImporter.cs` |
| 23 | Вывод данных на печать | `Wpf/Services/PrintService.cs` (FlowDocument + PrintDialog), отчёт текстовой таблицей `ResumeReport.ToText()` → report.txt, PDF через `PdfExporter.cs` |
| 24 | Журналирование | Serilog: `Core/Services/LoggingSetup.cs`, настройки в `Wpf/appsettings.json` (консоль, файл для Warning+, JSON-файл, ротация по дням и размеру) |
| 25 | Локализация и интернационализация | `.resx`: `Wpf/Properties/Resources.resx` + `Resources.en.resx`, `Core/Resources/Messages.resx` + `Messages.en.resx`; даты в PDF по культуре; переключение языка в меню |
| 26 | Обработка текста, регулярные выражения, валидация | `Core/Services/TextHelper.cs` (проверка e-mail и телефона, поиск в тексте, замены с группами), `Core/Validation/ResumeValidator.cs` (ручные проверки + DataAnnotations), свой атрибут `BelarusPhoneAttribute.cs` |

## ООП и MVVM

- Наследование и полиморфизм: `ResumeItem` → `WorkExperience`, `Education`, `Skill` (методы `GetDescription()`, `Validate()`).
- Интерфейс: `IResumeSerializer` → `JsonResumeSerializer`, `BinaryResumeSerializer`.
- MVVM: Model — `Core/Models`, View — `Wpf/Views/*.xaml` и `MainWindow.xaml`, ViewModel — `Wpf/ViewModels` (`MainViewModel`, `ListViewModel<T>`, `RelayCommand`).
