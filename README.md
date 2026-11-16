# ResumeBuilder — конструктор резюме с экспортом в PDF и QR-кодом

Курсовой проект по дисциплине «Конструирование программного обеспечения».
Автор: Лопатина Алина Николаевна, БНТУ, ФИТР, гр. 10701224.

Настольная программа для Windows, в которой можно заполнить резюме, проверить его
и сохранить в PDF с QR-кодом (визитка vCard), Word или Excel.

## Что умеет программа

- заполнение резюме: личные данные, о себе, опыт работы, образование, навыки;
- экспорт в **PDF с QR-кодом**, в Word и Excel; импорт опыта работы из таблицы Word;
- заполнение контактов из текста старого резюме (регулярные выражения);
- сводный отчёт (Excel + Word + txt), просмотр и печать;
- сохранение в JSON и в двоичный файл, черновик на случай закрытия;
- проверка введённых данных, русский и английский интерфейс, журнал работы (Serilog).

## Что нужно установить

| Что | Зачем |
|---|---|
| Windows 10 / 11 (x64 или ARM64) | программа на WPF работает только в Windows |
| Visual Studio 2022 (17.8+) или 2026 с компонентом «Разработка классических приложений .NET» | сборка и запуск |
| .NET 8 SDK | ставится вместе с Visual Studio |
| Git | получить исходный код |

Библиотеки (QuestPDF, QRCoder, ClosedXML, DocumentFormat.OpenXml, Serilog, xUnit) скачиваются
из NuGet автоматически при первой сборке.

## Сборка и запуск

**Visual Studio**

1. `git clone https://github.com/alinalopatina/ResumeBuilder.git`
2. Открыть `ResumeBuilder.sln`.
3. ПКМ по `ResumeBuilder.Wpf` → «Назначить автозагружаемым проектом» → **F5**.

**Командная строка**

```bash
dotnet build
dotnet run --project src/ResumeBuilder.Wpf
```

На Windows ARM программа собирается как x64 (у библиотеки QuestPDF нет версии для ARM64) — это настроено в `.csproj`.

## Тесты

```bash
dotnet test
```

Или в Visual Studio: «Тест → Обозреватель тестов → Запустить все». Подробно — [docs/testing.md](docs/testing.md).

## Документация API

XML-комментарии библиотеки `ResumeBuilder.Core` собираются в HTML инструментом DocFX:

```bash
dotnet tool install -g docfx
docfx docs/docfx/docfx.json --serve
```

После этого справочник открывается по адресу http://localhost:8080.

## Структура

```
src/ResumeBuilder.Core    — библиотека классов: модели и логика (сохранение, экспорт, проверка)
src/ResumeBuilder.Wpf     — интерфейс на WPF по шаблону MVVM (Views, ViewModels)
tests/ResumeBuilder.Tests — модульные тесты xUnit
docs/                     — документация (лабораторные, тесты, глоссарий, DocFX)
```

## Документы проекта

| Файл | Для кого и о чём |
|---|---|
| [CHANGELOG.md](CHANGELOG.md) | что менялось от версии к версии |
| [CONTRIBUTING.md](CONTRIBUTING.md) | правила веток, коммитов и пул-реквестов |
| [GLOSSARY.md](GLOSSARY.md) | словарь терминов предметной области |
| [docs/labs.md](docs/labs.md) | где в коде каждая лабораторная работа |
| [docs/testing.md](docs/testing.md) | тестовый план, TDD |
| [docs/audiences.md](docs/audiences.md) | целевые аудитории документации |
| [docs/docs-audit.md](docs/docs-audit.md) | аудит устаревшей документации |

## Лицензия

Проект распространяется по лицензии MIT — см. файл [LICENSE](LICENSE).
