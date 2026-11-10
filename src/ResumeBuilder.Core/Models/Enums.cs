namespace ResumeBuilder.Core.Models;

/// <summary>Категория навыка.</summary>
public enum SkillCategory
{
    Programming,
    Tools,
    SoftSkills,
    Other
}

/// <summary>Уровень владения иностранным языком по шкале CEFR.</summary>
public enum LanguageLevel
{
    A1,
    A2,
    B1,
    B2,
    C1,
    C2,
    Native
}

/// <summary>Шаблон оформления PDF-резюме.</summary>
public enum ResumeTemplate
{
    /// <summary>Классический одноколоночный шаблон.</summary>
    Classic,

    /// <summary>Современный шаблон с цветной боковой колонкой.</summary>
    Modern
}

/// <summary>Что кодировать в QR-код резюме.</summary>
public enum QrContentMode
{
    /// <summary>Визитная карточка vCard (имя, телефон, e-mail, сайт).</summary>
    VCard,

    /// <summary>Только ссылка на сайт / GitHub / портфолио.</summary>
    Website
}
