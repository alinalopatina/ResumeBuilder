using System.ComponentModel.DataAnnotations;
using ResumeBuilder.Core.Resources;

namespace ResumeBuilder.Core.Models;

/// <summary>Навык с уровнем от 1 до 5.</summary>
[Serializable]
public class Skill : ResumeItem
{
    [Required(ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Error_SkillNameRequired))]
    public string Name { get; set; } = "";

    [Range(1, 5, ErrorMessageResourceType = typeof(Messages), ErrorMessageResourceName = nameof(Messages.Error_SkillLevel))]
    public int Level { get; set; } = 3;

    public override string GetDescription() => $"{Name} ({Level}/5)";
}
