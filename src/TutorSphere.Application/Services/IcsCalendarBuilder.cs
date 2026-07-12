using System.Globalization;
using System.Text;
using TutorSphere.Application.DTOs.Lessons;

namespace TutorSphere.Application.Services;

public static class IcsCalendarBuilder
{
    public static string Build(string calendarName, IEnumerable<LessonDto> lessons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//TutorSphere//Calendar//FR");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine($"X-WR-CALNAME:{Escape(calendarName)}");

        foreach (var lesson in lessons)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{lesson.Id}@tutorsphere");
            sb.AppendLine($"DTSTAMP:{FormatUtc(DateTime.UtcNow)}");
            sb.AppendLine($"DTSTART:{FormatUtc(ToUtc(lesson.StartTime))}");
            sb.AppendLine($"DTEND:{FormatUtc(ToUtc(lesson.EndTime))}");
            sb.AppendLine($"SUMMARY:{Escape(lesson.Title)}");

            var descParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(lesson.Subject))
                descParts.Add($"Matière: {lesson.Subject}");
            if (!string.IsNullOrWhiteSpace(lesson.Description))
                descParts.Add(lesson.Description);
            if (!string.IsNullOrWhiteSpace(lesson.MeetingUrl))
                descParts.Add($"Lien: {lesson.MeetingUrl}");
            if (!string.IsNullOrWhiteSpace(lesson.Location))
                descParts.Add($"Lieu: {lesson.Location}");
            if (descParts.Count > 0)
                sb.AppendLine($"DESCRIPTION:{Escape(string.Join("\\n", descParts))}");

            if (!string.IsNullOrWhiteSpace(lesson.Location))
                sb.AppendLine($"LOCATION:{Escape(lesson.Location)}");
            else if (!string.IsNullOrWhiteSpace(lesson.MeetingUrl))
                sb.AppendLine($"LOCATION:{Escape(lesson.MeetingUrl)}");

            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static string FormatUtc(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
