using System.Text;
using ResumeBuilder.Core.Models;
using ResumeBuilder.Core.Text;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Формирует содержимое QR-кода: визитку vCard 3.0 или ссылку.
/// Работодатель сканирует QR с распечатанного резюме и сразу сохраняет контакт в телефон.
/// </summary>
public static class QrPayloadBuilder
{
    /// <summary>Строит строку для QR-кода по выбранному режиму.</summary>
    public static string Build(Resume resume) =>
        resume.QrMode == QrContentMode.Website && !string.IsNullOrWhiteSpace(resume.Personal.Website)
            ? TextFormatter.NormalizeUrl(resume.Personal.Website)
            : BuildVCard(resume.Personal);

    /// <summary>vCard 3.0 (RFC 2426). Спецсимволы экранируются регулярным выражением.</summary>
    public static string BuildVCard(PersonalInfo p)
    {
        var name = TextFormatter.CollapseSpaces(p.FullName);
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var last = parts.Length > 0 ? parts[0] : string.Empty;
        var first = parts.Length > 1 ? parts[1] : string.Empty;
        var middle = parts.Length > 2 ? parts[2] : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCARD");
        sb.AppendLine("VERSION:3.0");
        sb.AppendLine($"N:{TextFormatter.EscapeVCard(last)};{TextFormatter.EscapeVCard(first)};{TextFormatter.EscapeVCard(middle)};;");
        sb.AppendLine($"FN:{TextFormatter.EscapeVCard(name)}");
        if (!string.IsNullOrWhiteSpace(p.DesiredPosition)) sb.AppendLine($"TITLE:{TextFormatter.EscapeVCard(p.DesiredPosition)}");
        if (!string.IsNullOrWhiteSpace(p.Phone)) sb.AppendLine($"TEL;TYPE=CELL:{TextFormatter.NormalizePhone(p.Phone)}");
        if (!string.IsNullOrWhiteSpace(p.Email)) sb.AppendLine($"EMAIL:{p.Email.Trim()}");
        if (!string.IsNullOrWhiteSpace(p.Website)) sb.AppendLine($"URL:{TextFormatter.NormalizeUrl(p.Website)}");
        if (!string.IsNullOrWhiteSpace(p.City)) sb.AppendLine($"ADR;TYPE=HOME:;;;{TextFormatter.EscapeVCard(p.City)};;;");
        sb.Append("END:VCARD");
        return sb.ToString();
    }
}
