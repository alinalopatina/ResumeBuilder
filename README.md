# ResumeBuilder — конструктор резюме с экспортом в PDF и QR-кодом

![build](../../actions/workflows/build.yml/badge.svg)

Курсовой проект по дисциплине «Конструирование программного обеспечения».
Автор: **Лопатина Алина Николаевна**, БНТУ, ФИТР, группа 10701224. Руководитель: Ошуковская О. Э.

Настольное приложение на C# / WPF для составления резюме: заполнение разделов, проверка данных,
экспорт в **PDF с QR-кодом** (vCard или ссылка на портфолио), Word и Excel, сводный отчёт, печать,
двуязычный интерфейс (русский / английский).

## Возможности

| Функция | Где реализовано |
|---|---|
| Редактор резюме (личные данные, опыт, образование, навыки, языки) | `ResumeBuilder.Wpf` |
| Экспорт в PDF — два шаблона, QR-код, «публичная» версия с маскированием контактов | `Infrastructure/Pdf` |
| Экспорт в Word (абзацы + таблица + изображение QR) и **обратный импорт** таблицы из Word | `Infrastructure/Office` |
| Экспорт в Excel (жирная шапка, границы, закреплённая строка, автофильтр, формулы) | `Infrastructure/Office` |
| Сводный отчёт: одна агрегация → Excel, Word, PDF, TXT | `Core/Reports`, `Infrastructure/Reports` |
| Сохранение в JSON и собственный двоичный формат `.rbin`, автосохранение | `Infrastructure/Serialization` |
| Проверка данных: ручная + DataAnnotations + свои атрибуты | `Core/Validation` |
| «Умная вставка»: распознавание контактов из текста регулярными выражениями | `Core/Text` |
| Журналирование (Serilog): консоль, файл, JSON, аудит, ротация, CorrelationId | `Infrastructure/Logging` |
| Локализация ru/en (.resx), форматы дат/валют/склонений | `Core/Localization`, `Core/Resources` |
| Отмена действий (паттерн Memento), печать на принтер | `Core/History`, `Wpf/Services/PrintService.cs` |

## Требования

* Windows 10/11
* **Visual Studio 2022** (17.8+) или **Visual Studio 2026** с рабочей нагрузкой **«Разработка классических приложений .NET»**
* **.NET 8 SDK** (ставится вместе с Visual Studio)
* Git

Дополнительно ничего скачивать не нужно: все библиотеки подтягиваются из NuGet автоматически при первой сборке.

## Сборка и запуск

**Visual Studio:** открыть `ResumeBuilder.sln` → в «Обозревателе решений» ПКМ по `ResumeBuilder.Wpf` →
«Назначить автозагружаемым проектом» → **F5**.

**Командная строка:**

```bash
dotnet build
dotnet test
dotnet run --project src/ResumeBuilder.Wpf
```

**Консольная утилита** (кроссплатформенная, для демонстрации лабораторных и Docker):

```bash
cd src/ResumeBuilder.Cli
dotnet run -- help
dotnet run -- export samples/sample.resume.json output/cv.pdf
dotnet run -- export samples/sample.resume.json output/cv-en.pdf --lang en-US --template Classic --mask
dotnet run -- report samples/sample.resume.json output
dotnet run -- validate samples/sample.resume.json
dotnet run -- regex samples/old-resume.txt
dotnet run -- locale
dotnet run -- log-demo
dotnet run -- bench-serialize
dotnet run -- bench-excel 20000
```

## Структура решения

```
ResumeBuilder.sln
├─ src/
│  ├─ ResumeBuilder.Core            — пользовательская библиотека: модели, валидация, RegEx,
│  │                                   локализация, агрегация отчёта (без сторонних зависимостей)
│  ├─ ResumeBuilder.Infrastructure  — сериализация, PDF (QuestPDF), QR (QRCoder),
│  │                                   Excel (ClosedXML), Word (Open XML SDK), Serilog, DI
│  ├─ ResumeBuilder.Wpf             — графический интерфейс (MVVM)
│  └─ ResumeBuilder.Cli             — консольная утилита (будет упакована в Docker)
├─ tests/ResumeBuilder.Tests        — unit-тесты xUnit
└─ docs/                            — архитектура, соответствие лабораторным
```

Подробнее: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/LABS.md](docs/LABS.md) — какая лабораторная где реализована.

## Используемые библиотеки

| Пакет | Назначение | Лицензия |
|---|---|---|
| QuestPDF | генерация PDF | Community (бесплатно для учебных проектов) |
| QRCoder | QR-код | MIT |
| ClosedXML | Excel | MIT |
| DocumentFormat.OpenXml | Word, потоковая запись Excel | MIT |
| Serilog (+ Sinks.File/Console, Settings.Configuration, Formatting.Compact) | журналирование | Apache-2.0 |
| Microsoft.Extensions.* | конфигурация, DI, абстракции журнала | MIT |
| xUnit | unit-тесты | Apache-2.0 |

Office Interop **не используется**: он требует установленного Office, не потокобезопасен и не работает в Docker.

## Где лежат файлы программы

* Настройки и автосохранение: `%APPDATA%\ResumeBuilder\`
* Журналы: `%LOCALAPPDATA%\ResumeBuilder\logs\` (`app-*.log` — предупреждения и ошибки, `app-*.json` — структурированный журнал, `audit-*.log` — аудит)

## Лицензия

[MIT](LICENSE)
