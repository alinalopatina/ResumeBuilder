using ResumeBuilder.Core.Resources;

namespace ResumeBuilder.Core.Models;

/// <summary>Учебное заведение.</summary>
public class Education : ResumeItem
{
    public string Institution { get; set; } = "";
    public string Specialty { get; set; } = "";
    public int StartYear { get; set; } = DateTime.Today.Year;
    public int? EndYear { get; set; }

    public override string GetDescription()
    {
        string years = EndYear == null ? $"{StartYear} – ..." : $"{StartYear} – {EndYear}";
        return $"{Institution}, {Specialty} ({years})";
    }

    public override void Validate(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(Institution))
            errors.Add(Messages.Error_InstitutionRequired);
        if (EndYear != null && EndYear < StartYear)
            errors.Add(string.Format(Messages.Error_EndBeforeStart, Institution));
    }
}
