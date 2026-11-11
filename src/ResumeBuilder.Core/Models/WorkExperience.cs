using ResumeBuilder.Core.Resources;

namespace ResumeBuilder.Core.Models;

/// <summary>Место работы.</summary>
[Serializable]
public class WorkExperience : ResumeItem
{
    public string Company { get; set; } = "";
    public string Position { get; set; } = "";
    public DateTime StartDate { get; set; } = DateTime.Today;
    /// <summary>null — работаю здесь сейчас.</summary>
    public DateTime? EndDate { get; set; }
    public string Description { get; set; } = "";

    /// <summary>Сколько месяцев проработал(а).</summary>
    public int GetMonths(DateTime today)
    {
        DateTime end = EndDate ?? today;
        int months = (end.Year - StartDate.Year) * 12 + end.Month - StartDate.Month;
        return Math.Max(months, 0);
    }

    public override string GetDescription()
    {
        string end = EndDate?.ToString("MM.yyyy") ?? Messages.Present;
        return $"{Position}, {Company} ({StartDate:MM.yyyy} – {end})";
    }

    public override void Validate(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(Company))
            errors.Add(Messages.Error_CompanyRequired);
        if (EndDate != null && EndDate < StartDate)
            errors.Add(string.Format(Messages.Error_EndBeforeStart, Company));
        if (StartDate > DateTime.Today)
            errors.Add(string.Format(Messages.Error_StartInFuture, Company));
    }
}
