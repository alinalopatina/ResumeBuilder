# Архитектура ResumeBuilder

Диаграммы в формате Mermaid — GitHub рисует их прямо на странице. Для записки их можно
открыть в https://mermaid.live и экспортировать в PNG/SVG.

## 1. Слои и зависимости

Зависимости направлены внутрь: ядро (`Core`) ничего не знает о форматах файлов и интерфейсе.

```mermaid
flowchart TB
    subgraph UI["Представление"]
        WPF["ResumeBuilder.Wpf<br/>MVVM: MainWindow, MainViewModel,<br/>LocalizationManager, PrintService"]
        CLI["ResumeBuilder.Cli<br/>консольные команды"]
    end
    subgraph INF["ResumeBuilder.Infrastructure"]
        SER["Serialization<br/>JSON, Binary, ResumeFileService"]
        PDF["Pdf<br/>QuestPDF-шаблоны"]
        OFF["Office<br/>Excel, Word, импорт"]
        REP["Reports<br/>рендереры отчёта"]
        LOG["Logging<br/>Serilog, аудит"]
        QR["Qr<br/>QRCoder"]
    end
    subgraph CORE["ResumeBuilder.Core (пользовательская библиотека)"]
        MOD["Models"]
        VAL["Validation"]
        TXT["Text (RegEx)"]
        LOC["Localization + .resx"]
        AGG["Reports (агрегация)"]
        ABS["Abstractions (интерфейсы)"]
    end
    WPF --> INF
    CLI --> INF
    WPF --> CORE
    CLI --> CORE
    INF --> CORE
```

## 2. Диаграмма классов предметной области

```mermaid
classDiagram
    class Resume {
        +int SchemaVersion
        +Guid Id
        +PersonalInfo Personal
        +string Summary
        +List~WorkExperience~ Experience
        +List~Education~ Education
        +List~Skill~ Skills
        +List~LanguageSkill~ Languages
        +ResumeTemplate Template
        +QrContentMode QrMode
        +string CultureName
        -string FilePath
        +AllItems() IEnumerable~ResumeItem~
    }
    class PersonalInfo {
        +string FullName
        +string DesiredPosition
        +string Email
        +string Phone
        +DateTime? BirthDate
        +string Website
        +decimal? DesiredSalary
    }
    class ResumeItem {
        <<abstract>>
        +Guid Id
        +SectionKey* string
        +GetSummary(culture)* string
        +Validate(path, today) IEnumerable~ValidationIssue~
    }
    class WorkExperience {
        +string Company
        +string Position
        +DateTime StartDate
        +DateTime? EndDate
        +GetDurationMonths(today) int
    }
    class Education
    class Skill {
        +string Name
        +SkillCategory Category
        +int Level
    }
    class LanguageSkill
    Resume "1" *-- "1" PersonalInfo
    Resume "1" *-- "*" WorkExperience
    Resume "1" *-- "*" Education
    Resume "1" *-- "*" Skill
    Resume "1" *-- "*" LanguageSkill
    ResumeItem <|-- WorkExperience
    ResumeItem <|-- Education
    ResumeItem <|-- Skill
    ResumeItem <|-- LanguageSkill
```

## 3. Стратегии экспорта и сериализации

```mermaid
classDiagram
    class IResumeExporter {
        <<interface>>
        +FormatName string
        +FileExtension string
        +Export(resume, stream, options)
    }
    class IResumeSerializer {
        <<interface>>
        +Serialize(resume, stream)
        +Deserialize(stream) Resume
    }
    class IReportRenderer {
        <<interface>>
        +Render(report, stream)
    }
    class PdfResumeTemplate {
        <<abstract>>
        +Compose(container)
        #ConfigurePage(page)
        #ComposeContent(container)*
        #ComposeFooter(container)
    }
    IResumeExporter <|.. PdfResumeExporter
    IResumeExporter <|.. WordResumeExporter
    IResumeExporter <|.. ExcelExperienceExporter
    IResumeSerializer <|.. JsonResumeSerializer
    IResumeSerializer <|.. BinaryResumeSerializer
    IReportRenderer <|.. ExcelReportRenderer
    IReportRenderer <|.. WordReportRenderer
    IReportRenderer <|.. PdfReportRenderer
    IReportRenderer <|.. TextReportRenderer
    PdfResumeTemplate <|-- ClassicPdfTemplate
    PdfResumeTemplate <|-- ModernPdfTemplate
    PdfResumeExporter ..> PdfResumeTemplate : выбирает шаблон
    PdfResumeExporter ..> IQrCodeGenerator
```

## 4. Сводный отчёт без дублирования подсчёта

```mermaid
flowchart LR
    R[Resume] --> S[ResumeStatisticsService<br/>считает один раз]
    S --> B[ReportDocumentBuilder<br/>раскладывает по разделам на нужном языке]
    B --> D[(ReportDocument<br/>формат-независимый)]
    D --> X[ExcelReportRenderer]
    D --> W[WordReportRenderer]
    D --> P[PdfReportRenderer]
    D --> T[TextReportRenderer]
```

## 5. Алгоритм экспорта в PDF (для схемы алгоритма в записке)

```mermaid
flowchart TD
    A([Нажата «Экспорт в PDF»]) --> B[Синхронизировать таблицы интерфейса с моделью]
    B --> C{Валидация:<br/>есть ошибки?}
    C -- да --> D{Пользователь подтвердил<br/>экспорт с ошибками?}
    D -- нет --> Z([Конец])
    D -- да --> E
    C -- нет --> E[Диалог «Сохранить как»]
    E --> F{Файл выбран?}
    F -- нет --> Z
    F -- да --> G{Добавлять QR?}
    G -- да --> H[QrPayloadBuilder: vCard или URL<br/>QRCoder → PNG]
    G -- нет --> I
    H --> I{Маскировать контакты?}
    I -- да --> J[RegEx-замены e-mail и телефона]
    I -- нет --> K
    J --> K[Выбрать шаблон: Classic / Modern]
    K --> L[QuestPDF: Compose → GeneratePdf]
    L --> M[Запись в журнал и аудит]
    M --> N{Открыть файл?}
    N -- да --> O[Открыть в программе по умолчанию]
    N -- нет --> Z
    O --> Z
```

## 6. Алгоритм «умной вставки» (RegEx)

```mermaid
flowchart TD
    A([Текст пользователя]) --> B[EmailSearch → e-mail, нижний регистр, без повторов]
    B --> C[BelarusPhone → нормализация по группам code/p1/p2/p3]
    C --> D[UrlSearch + lookbehind GitHubLogin]
    D --> E[TelegramHandle — негативный lookbehind исключает e-mail]
    E --> F[DateDdMmYyyy → TryParseExact отсекает 31.02]
    F --> G[WorkPeriod: ММ.ГГГГ – ММ.ГГГГ / н.в.]
    G --> H[SkillsLine → Split по , ; •]
    H --> I[KeyValueLine: ФИО, Город, Должность]
    I --> J{Пользователь нажал<br/>«Применить»?}
    J -- да --> K[Снимок для отмены → заполнить только пустые поля]
    J -- нет --> L([Показать найденное])
    K --> L
```

## 7. Паттерны проектирования

| Паттерн | Класс | Зачем |
|---|---|---|
| Строитель | `ResumeFluentBuilder` | пошаговое создание резюме (тесты, пример) |
| Хранитель (Memento) | `ResumeHistory`, `ResumeMemento` | отмена действий |
| Шаблонный метод | `PdfResumeTemplate` | общий скелет страницы, разное содержимое |
| Стратегия | `IResumeSerializer`, `IResumeExporter`, `IReportRenderer`, `IResumeValidator` | взаимозаменяемые алгоритмы |
| Компоновщик | `CompositeResumeValidator` | несколько валидаторов как один |
| Фабрика | `ResumeFileService.GetSerializer` | выбор сериализатора по расширению |
| Наблюдатель | `INotifyPropertyChanged`, `ILocalizer.CultureChanged` | обновление интерфейса |
| Команда | `RelayCommand` | действия кнопок и меню |
| Одиночка | `LocalizationManager.Instance` | единый источник строк для XAML |
| Внедрение зависимостей | `ServiceRegistration.AddResumeBuilder` | слабая связанность, тестируемость |
