# Генератор Strings.resx / Strings.en.resx из одного словаря (гарантирует одинаковый набор ключей)
import sys, html
S = {
"App_Title": ("Конструктор резюме", "Resume Builder"),
"App_Untitled": ("Новое резюме", "Untitled resume"),
"App_Greeting": ("Добро пожаловать в конструктор резюме!", "Welcome to Resume Builder!"),
# меню
"Menu_File": ("_Файл", "_File"), "Menu_New": ("Создать", "New"), "Menu_Sample": ("Загрузить пример", "Load sample"),
"Menu_Open": ("Открыть…", "Open…"), "Menu_Save": ("Сохранить", "Save"), "Menu_SaveAs": ("Сохранить как…", "Save as…"),
"Menu_Print": ("Печать…", "Print…"), "Menu_Exit": ("Выход", "Exit"), "Menu_Edit": ("_Правка", "_Edit"),
"Menu_Undo": ("Отменить", "Undo"), "Menu_Normalize": ("Привести контакты к единому виду", "Normalize contacts"),
"Menu_Validate": ("Проверить резюме", "Validate resume"), "Menu_Export": ("_Экспорт", "E_xport"),
"Menu_ExportPdf": ("Экспорт в PDF с QR-кодом…", "Export to PDF with QR code…"),
"Menu_ExportWord": ("Экспорт в Word…", "Export to Word…"), "Menu_ExportExcel": ("Экспорт в Excel…", "Export to Excel…"),
"Menu_ExportReport": ("Сводный отчёт…", "Summary report…"), "Menu_ImportWord": ("Импорт опыта из Word…", "Import experience from Word…"),
"Menu_Language": ("_Язык", "_Language"),
# вкладки и панели
"Tab_Personal": ("Личные данные", "Personal info"), "Tab_SmartPaste": ("Умная вставка", "Smart paste"),
"Panel_Export": ("Параметры экспорта", "Export settings"), "Panel_Qr": ("QR-код", "QR code"),
"Panel_Validation": ("Проверка", "Validation"), "Qr_Refresh": ("Обновить", "Refresh"),
# поля
"Field_FullName": ("ФИО", "Full name"), "Field_DesiredPosition": ("Желаемая должность", "Desired position"),
"Field_Email": ("E-mail", "E-mail"), "Field_Phone": ("Телефон", "Phone"), "Field_City": ("Город", "City"),
"Field_BirthDate": ("Дата рождения", "Date of birth"), "Field_Age": ("Возраст", "Age"),
"Field_Website": ("Сайт / GitHub", "Website / GitHub"), "Field_Salary": ("Желаемая зарплата", "Expected salary"),
"Field_Photo": ("Фото (путь к файлу)", "Photo (file path)"), "Field_Template": ("Шаблон PDF", "PDF template"),
"Field_DocumentLanguage": ("Язык документа", "Document language"), "Field_QrMode": ("Содержимое QR-кода", "QR code content"),
"Field_IncludeQr": ("Добавить QR-код", "Include QR code"), "Field_MaskContacts": ("Скрыть часть контактов (публичная версия)", "Mask contacts (public version)"),
# подсказки
"Hint_Summary": ("Кратко о себе: 3–5 предложений о профессиональных интересах и сильных сторонах.", "A short summary: 3–5 sentences about your professional interests and strengths."),
"Hint_Grid": ("Добавляйте строки в конце таблицы, удаляйте — клавишей Delete.", "Add rows at the bottom of the table, delete with the Delete key."),
"Hint_SmartPaste": ("Вставьте текст старого резюме или подписи из письма — программа найдёт e-mail, телефоны, ссылки, даты и навыки с помощью регулярных выражений.", "Paste the text of an old resume or an e-mail signature — the program will find e-mails, phones, links, dates and skills using regular expressions."),
"SmartPaste_Analyze": ("Распознать", "Analyze"), "SmartPaste_Apply": ("Применить к резюме", "Apply to resume"),
"SmartPaste_Found": ("Найдено:", "Found:"), "SmartPaste_NothingFound": ("Ничего не найдено", "Nothing found"),
"SmartPaste_RegexPreview": ("Замены регулярными выражениями (предпросмотр):", "Regex replacements (preview):"),
"Regex_PhonesNormalized": ("Телефоны в едином формате", "Phones normalized"),
"Regex_DatesIso": ("Даты в формате ISO", "Dates in ISO format"),
"Regex_Masked": ("Публичная версия (маскирование)", "Public version (masked)"),
# разделы
"Section_Summary": ("О себе", "Summary"), "Section_Experience": ("Опыт работы", "Work experience"),
"Section_Education": ("Образование", "Education"), "Section_Skills": ("Навыки", "Skills"),
"Section_Languages": ("Языки", "Languages"), "Section_Contacts": ("Контакты", "Contacts"),
# столбцы
"Column_Company": ("Компания", "Company"), "Column_Position": ("Должность", "Position"), "Column_City": ("Город", "City"),
"Column_Start": ("Начало", "Start"), "Column_End": ("Окончание", "End"), "Column_Months": ("Месяцев", "Months"),
"Column_Share": ("Доля стажа, %", "Share, %"), "Column_Description": ("Обязанности и достижения", "Responsibilities and achievements"),
"Column_Institution": ("Учебное заведение", "Institution"), "Column_Degree": ("Степень", "Degree"),
"Column_Specialty": ("Специальность", "Specialty"), "Column_StartYear": ("Год начала", "Start year"),
"Column_EndYear": ("Год окончания", "End year"), "Column_Skill": ("Навык", "Skill"), "Column_Category": ("Категория", "Category"),
"Column_Level": ("Уровень", "Level"), "Column_Language": ("Язык", "Language"), "Column_Count": ("Количество", "Count"),
"Column_AvgLevel": ("Средний уровень", "Average level"), "Column_MaxLevel": ("Максимальный уровень", "Max level"),
# перечисления
"SkillCategory_Programming": ("Программирование", "Programming"), "SkillCategory_Tools": ("Инструменты", "Tools"),
"SkillCategory_SoftSkills": ("Личные качества", "Soft skills"), "SkillCategory_Other": ("Другое", "Other"),
"LanguageLevel_A1": ("A1 — начальный", "A1 — Beginner"), "LanguageLevel_A2": ("A2 — элементарный", "A2 — Elementary"),
"LanguageLevel_B1": ("B1 — средний", "B1 — Intermediate"), "LanguageLevel_B2": ("B2 — выше среднего", "B2 — Upper-intermediate"),
"LanguageLevel_C1": ("C1 — продвинутый", "C1 — Advanced"), "LanguageLevel_C2": ("C2 — в совершенстве", "C2 — Proficient"),
"LanguageLevel_Native": ("родной", "native"),
# склонения
"Plural_Year_One": ("год", "year"), "Plural_Year_Few": ("года", "years"), "Plural_Year_Many": ("лет", "years"),
"Plural_Month_One": ("месяц", "month"), "Plural_Month_Few": ("месяца", "months"), "Plural_Month_Many": ("месяцев", "months"),
"Period_Present": ("н. в.", "present"),
# PDF
"Pdf_Resume": ("Резюме", "Resume"), "Pdf_QrVCard": ("Отсканируйте, чтобы сохранить контакт", "Scan to save the contact"),
"Pdf_QrWebsite": ("Отсканируйте, чтобы открыть портфолио", "Scan to open the portfolio"),
# отчёт
"Report_Title": ("Сводный отчёт по резюме", "Resume summary report"), "Report_Generated": ("Сформировано", "Generated"),
"Report_General": ("Общие показатели", "Key figures"), "Report_TotalExperience": ("Общий стаж", "Total experience"),
"Report_JobsCount": ("Мест работы", "Jobs"), "Report_AverageJob": ("Средняя длительность работы", "Average job duration"),
"Report_LongestJob": ("Самое долгое место работы", "Longest job"), "Report_SkillsCount": ("Навыков", "Skills"),
"Report_AverageSkill": ("Средний уровень навыков (из 5)", "Average skill level (of 5)"),
"Report_Languages": ("Иностранных языков", "Languages"), "Report_B2Plus": ("из них B2 и выше", "B2 or higher"),
"Report_Completeness": ("Заполненность резюме", "Resume completeness"), "Report_Total": ("Итого", "Total"),
"Report_Conclusion": ("Вывод", "Conclusion"),
"Conclusion_Experience_Junior": ("Опыт работы меньше года — резюме подходит для стажировки или позиции Junior; стоит подробнее описать учебные проекты.", "Less than a year of experience — the resume fits an internship or junior position; describe study projects in more detail."),
"Conclusion_Experience_Middle": ("Стаж от одного до трёх лет — можно претендовать на позиции Junior+/Middle.", "One to three years of experience — Junior+/Middle positions are realistic."),
"Conclusion_Experience_Senior": ("Стаж более трёх лет — стоит акцентировать достижения и лидерские навыки.", "More than three years of experience — emphasize achievements and leadership."),
"Conclusion_Skills_Strong": ("Средний уровень навыков высокий.", "The average skill level is high."),
"Conclusion_Skills_Few": ("Указано мало навыков — добавьте хотя бы 5 ключевых.", "Too few skills — add at least 5 key ones."),
"Conclusion_Incomplete": ("Резюме заполнено на {0}% — заполните недостающие разделы.", "The resume is {0}% complete — fill in the missing sections."),
"Conclusion_Complete": ("Резюме заполнено полностью.", "The resume is complete."),
# состояние
"Status_New": ("Создано новое резюме", "New resume created"), "Status_SampleLoaded": ("Загружен пример резюме", "Sample resume loaded"),
"Status_Opened": ("Открыт файл {0}", "Opened {0}"), "Status_Saved": ("Сохранено: {0}", "Saved: {0}"),
"Status_Exported": ("Экспортировано: {0}", "Exported: {0}"), "Status_ReportSaved": ("Отчёт сохранён в {0} форматах: {1}", "Report saved in {0} formats: {1}"),
"Status_Printed": ("Резюме отправлено на печать", "Resume sent to printer"),
"Status_ValidOk": ("Проверка пройдена: ошибок нет", "Validation passed: no issues"),
"Status_ValidIssues": ("Ошибок: {0}, предупреждений: {1}", "Errors: {0}, warnings: {1}"),
"Status_Undone": ("Действие отменено", "Action undone"), "Status_Analyzed": ("Распознано элементов: {0}", "Recognized items: {0}"),
"Status_Applied": ("Заполнено полей: {0}", "Fields filled: {0}"), "Status_Normalized": ("Контакты приведены к единому виду", "Contacts normalized"),
"Status_AutosaveRestored": ("Восстановлен черновик из автосохранения", "Draft restored from autosave"),
"Undo_New": ("создание нового резюме", "new resume"), "Undo_Sample": ("загрузка примера", "load sample"),
"Undo_Import": ("импорт из Word", "import from Word"), "Undo_SmartPaste": ("умная вставка", "smart paste"),
"Undo_Normalize": ("нормализация контактов", "normalize contacts"),
"Confirm_ExportWithErrors": ("В резюме есть ошибки (см. панель «Проверка»). Всё равно экспортировать?", "The resume has errors (see the Validation panel). Export anyway?"),
"Confirm_OpenFile": ("Файл сохранён. Открыть его?", "File saved. Open it?"),
"Confirm_OpenFolder": ("Отчёт сохранён. Открыть папку?", "Report saved. Open the folder?"),
"Error_Generic": ("Произошла ошибка: {0}", "An error occurred: {0}"),
# импорт
"Import_NoBody": ("Документ пуст или повреждён", "The document is empty or damaged"),
"Import_TableNotFound": ("В документе не найдена таблица опыта работы (нужны столбцы «Компания», «Должность», «Начало»)", "No work experience table found (columns Company, Position, Start are required)"),
"Import_RowMissingFields": ("Строка {0}: не заполнены компания или должность", "Row {0}: company or position is empty"),
"Import_RowBadDate": ("Строка {0}: не удалось распознать дату «{1}» (ожидается ММ.ГГГГ)", "Row {0}: cannot parse date \"{1}\" (expected MM.YYYY)"),
"Import_Result": ("Импортировано записей: {0}. Ошибок: {1}.", "Imported records: {0}. Errors: {1}."),
# валидация
"Validation_FullNameRequired": ("Укажите ФИО", "Full name is required"),
"Validation_FullNameLength": ("ФИО должно содержать от 2 до 100 символов", "Full name must be 2–100 characters long"),
"Validation_FullNameFormat": ("ФИО может содержать только буквы, пробелы, дефис и апостроф", "Full name may contain only letters, spaces, hyphens and apostrophes"),
"Validation_DesiredPositionRequired": ("Укажите желаемую должность", "Desired position is required"),
"Validation_EmailRequired": ("Укажите e-mail", "E-mail is required"),
"Validation_EmailFormat": ("E-mail указан в неверном формате (пример: name@mail.by)", "Invalid e-mail format (example: name@mail.com)"),
"Validation_PhoneFormat": ("Телефон указан в неверном формате (пример: +375 29 123-45-67)", "Invalid phone format (example: +375 29 123-45-67)"),
"Validation_UrlFormat": ("Ссылка указана в неверном формате (пример: https://github.com/user)", "Invalid link format (example: https://github.com/user)"),
"Validation_BirthDateFuture": ("Дата рождения не может быть в будущем", "Date of birth cannot be in the future"),
"Validation_MinAge": ("Возраст должен быть от 14 до 100 лет", "Age must be between 14 and 100"),
"Validation_SalaryRange": ("Зарплата должна быть от 0 до 1 000 000", "Salary must be between 0 and 1,000,000"),
"Validation_CurrencyFormat": ("Код валюты — три латинские заглавные буквы (BYN, USD, EUR)", "Currency code must be three capital letters (BYN, USD, EUR)"),
"Validation_TooLong": ("Слишком длинное значение", "Value is too long"),
"Validation_CompanyRequired": ("Укажите название компании", "Company is required"),
"Validation_PositionRequired": ("Укажите должность", "Position is required"),
"Validation_InstitutionRequired": ("Укажите учебное заведение", "Institution is required"),
"Validation_YearRange": ("Год должен быть в диапазоне 1950–2100", "Year must be between 1950 and 2100"),
"Validation_SkillNameRequired": ("Укажите название навыка", "Skill name is required"),
"Validation_SkillLevelRange": ("Уровень навыка — от 1 до 5", "Skill level must be 1–5"),
"Validation_LanguageRequired": ("Укажите язык", "Language is required"),
"Validation_StartInFuture": ("Дата начала не может быть в будущем", "Start date cannot be in the future"),
"Validation_EndBeforeStart": ("Дата окончания раньше даты начала", "End date is before start date"),
"Validation_EndInFuture": ("Дата окончания в будущем — если работаете сейчас, оставьте поле пустым", "End date is in the future — leave it empty if you still work there"),
"Validation_DescriptionEmpty": ("Опишите обязанности и достижения", "Describe responsibilities and achievements"),
"Validation_NoContactForQr": ("Не указаны ни телефон, ни сайт — QR-код будет малоинформативным", "Neither phone nor website is set — the QR code will be of little use"),
"Validation_NoExperienceNoEducation": ("Добавьте хотя бы одно место работы или учёбы", "Add at least one job or education entry"),
"Validation_NoSkills": ("Не указано ни одного навыка", "No skills specified"),
"Validation_DuplicateSkill": ("Навык «{0}» указан несколько раз", "Skill \"{0}\" is listed more than once"),
"Validation_SummaryEmpty": ("Раздел «О себе» не заполнен", "The Summary section is empty"),
"Validation_SummaryTooLong": ("Раздел «О себе» слишком длинный (больше 1500 символов)", "The Summary is too long (over 1500 characters)"),
"Validation_OverlappingJobs": ("Периоды работы в «{0}» и «{1}» пересекаются", "Employment periods at \"{0}\" and \"{1}\" overlap"),
}

HEADER = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <!-- Файл сгенерирован из единого словаря (docs/tools/gen_resx.py), чтобы наборы ключей ru/en совпадали. -->
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'''

def write(path, idx):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(HEADER)
        for k in sorted(S):
            v = html.escape(S[k][idx], quote=False)
            f.write(f'  <data name="{k}" xml:space="preserve">\n    <value>{v}</value>\n  </data>\n')
        f.write('</root>\n')

out = sys.argv[1]
write(out + '/Strings.resx', 0)
write(out + '/Strings.en.resx', 1)
print(len(S), 'keys')
if len(sys.argv) > 2:
    used = set(open(sys.argv[2]).read().split())
    missing = sorted(k for k in used if k not in S and k not in ('Plural_Year','Plural_Month','RESUMEBUILDER_ENVIRONMENT'))
    print('MISSING:', missing)
