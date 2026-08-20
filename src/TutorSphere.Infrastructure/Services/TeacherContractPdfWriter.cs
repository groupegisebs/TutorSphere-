using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TutorSphere.Application.Services;

namespace TutorSphere.Infrastructure.Services;

public sealed class TeacherContractPdfWriter(IWebHostEnvironment env) : ITeacherContractPdfWriter
{
    static TeacherContractPdfWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<(string RelativePath, string Sha256)> WriteSignedPdfAsync(
        TeacherContractPdfModel model,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(env.ContentRootPath, "secure-contracts");
        Directory.CreateDirectory(dir);
        var safe = string.Concat(model.ContractNumber.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        var fileName = $"{safe}.pdf";
        var path = Path.Combine(dir, fileName);

        byte[]? signature = DecodePng(model.SignaturePngBase64);
        var qrPng = BuildQrPng(model.VerificationUrl);

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken3));
                page.Header().Column(col =>
                {
                    col.Item().Text("TutorSphere").Bold().FontSize(16).FontColor("#5831E0");
                    col.Item().Text("Contrat de collaboration avec un enseignant indépendant").Bold().FontSize(13);
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text($"N° {model.ContractNumber}  ·  Version {model.Version}  ·  {model.Language.ToUpperInvariant()}");
                        row.ConstantItem(72).Height(72).Image(qrPng);
                    });
                    col.Item().LineHorizontal(1).LineColor("#5831E0");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);
                    foreach (var (title, body) in model.Sections)
                    {
                        col.Item().Text(title).Bold().FontSize(11).FontColor("#5831E0");
                        col.Item().Text(StripMd(body)).FontSize(9.5f).LineHeight(1.35f);
                    }

                    col.Item().PaddingTop(12).Text("Acceptation et signature").Bold().FontSize(12).FontColor("#5831E0");
                    col.Item().Text($"Pour le Groupe : {model.GroupSignatoryName} — {model.GroupSignatoryRole}");
                    col.Item().Text($"Pour l’Enseignant : {model.TeacherName}");
                    col.Item().Text("Mention : « J’ai lu, compris et accepté toutes les conditions du présent contrat. »");
                    col.Item().Text($"Date et heure (UTC) : {model.SignedAtUtc:yyyy-MM-dd HH:mm:ss}");
                    if (signature is { Length: > 0 })
                    {
                        col.Item().Text("Signature électronique de l’enseignant :").Bold();
                        col.Item().Width(220).Height(80).Image(signature);
                    }
                    col.Item().PaddingTop(8).Text($"Référence de vérification : {model.VerificationCode}").Bold();
                    col.Item().Text($"Empreinte SHA-256 : {model.DocumentHashPlaceholder}").FontSize(8);
                    col.Item().Text($"Vérification : {model.VerificationUrl}").FontSize(8);
                    col.Item().Text("Ce document est définitif et non modifiable. Toute altération est détectable par l’empreinte numérique.").Italic().FontSize(8);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"{model.ContractNumber} · page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();

        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        File.WriteAllBytes(path, bytes);
        return Task.FromResult(($"secure-contracts/{fileName}", hash));
    }

    public Task<byte[]?> ReadPdfAsync(string relativePath, CancellationToken ct = default)
    {
        var safe = Path.GetFileName(relativePath.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safe))
            return Task.FromResult<byte[]?>(null);
        var path = Path.Combine(env.ContentRootPath, "secure-contracts", safe);
        if (!File.Exists(path))
            return Task.FromResult<byte[]?>(null);
        return Task.FromResult<byte[]?>(File.ReadAllBytes(path));
    }

    private static byte[] BuildQrPng(string url)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(6);
    }

    private static byte[]? DecodePng(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        var comma = dataUrl.IndexOf(',');
        var b64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
        try { return Convert.FromBase64String(b64); }
        catch { return null; }
    }

    private static string StripMd(string body) =>
        body.Replace("**", "").Replace("### ", "").Replace("• ", "• ");
}
