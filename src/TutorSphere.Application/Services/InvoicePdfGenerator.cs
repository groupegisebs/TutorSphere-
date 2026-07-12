using System.Text;

namespace TutorSphere.Application.Services;

/// <summary>Génère une facture PDF minimale (PDF 1.4, Helvetica) sans dépendance externe.</summary>
public static class InvoicePdfGenerator
{
    public static byte[] Generate(
        string invoiceNumber,
        string parentName,
        string? studentName,
        string? tutorName,
        string description,
        decimal amount,
        string currency,
        DateTime issuedAt,
        DateTime? paidAt,
        string statusLabel)
    {
        var lines = new List<string>
        {
            "TutorSphere — Facture",
            "",
            $"N° {invoiceNumber}",
            $"Date d'émission : {issuedAt:dd/MM/yyyy}",
            paidAt is null ? "" : $"Payée le : {paidAt:dd/MM/yyyy}",
            $"Statut : {statusLabel}",
            "",
            $"Client : {Sanitize(parentName)}",
            string.IsNullOrWhiteSpace(studentName) ? "" : $"Élève : {Sanitize(studentName)}",
            string.IsNullOrWhiteSpace(tutorName) ? "" : $"Tuteur : {Sanitize(tutorName)}",
            "",
            "────────────────────────────────",
            Sanitize(description),
            $"Montant : {amount:0.00} {currency.ToUpperInvariant()}",
            "────────────────────────────────",
            "",
            "Merci pour votre confiance.",
            "Document généré automatiquement par TutorSphere."
        }.Where(l => l is not null).ToList();

        return BuildPdf(lines!);
    }

    private static string Sanitize(string value)
    {
        // PDF WinAnsi-ish: strip non-latin1 for Helvetica
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormKD))
        {
            if (ch is >= (char)32 and <= (char)126 or >= (char)160 and <= (char)255)
                sb.Append(ch);
            else if (ch is '’' or '‘')
                sb.Append('\'');
            else if (ch is '–' or '—')
                sb.Append('-');
            else if (char.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append('?');
        }
        return sb.ToString();
    }

    private static byte[] BuildPdf(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 12 Tf");
        content.AppendLine("50 780 Td");
        content.AppendLine("16 TL");

        for (var i = 0; i < lines.Count; i++)
        {
            var escaped = EscapePdf(lines[i]);
            if (i == 0)
                content.AppendLine($"({escaped}) Tj");
            else
                content.AppendLine($"T* ({escaped}) Tj");
        }

        content.AppendLine("ET");
        var stream = content.ToString();
        var streamBytes = Encoding.Latin1.GetBytes(stream);

        var objects = new List<byte[]>();
        objects.Add(Encoding.Latin1.GetBytes("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n"));
        objects.Add(Encoding.Latin1.GetBytes("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n"));
        objects.Add(Encoding.Latin1.GetBytes(
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n"));
        objects.Add(Encoding.Latin1.GetBytes(
            $"4 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n{stream}endstream\nendobj\n"));
        objects.Add(Encoding.Latin1.GetBytes(
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n"));

        using var ms = new MemoryStream();
        var header = Encoding.Latin1.GetBytes("%PDF-1.4\n");
        ms.Write(header);

        var offsets = new List<long> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(ms.Position);
            ms.Write(obj);
        }

        var xrefPos = ms.Position;
        var xref = new StringBuilder();
        xref.AppendLine("xref");
        xref.AppendLine($"0 {objects.Count + 1}");
        xref.AppendLine("0000000000 65535 f ");
        for (var i = 1; i <= objects.Count; i++)
            xref.AppendLine($"{offsets[i]:D10} 00000 n ");

        xref.AppendLine("trailer");
        xref.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        xref.AppendLine("startxref");
        xref.AppendLine(xrefPos.ToString());
        xref.AppendLine("%%EOF");

        var xrefBytes = Encoding.Latin1.GetBytes(xref.ToString());
        ms.Write(xrefBytes);
        return ms.ToArray();
    }

    private static string EscapePdf(string text) =>
        text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
