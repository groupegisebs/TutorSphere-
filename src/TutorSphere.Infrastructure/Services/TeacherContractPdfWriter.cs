using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TutorSphere.Application.Common;
using TutorSphere.Application.Services;

namespace TutorSphere.Infrastructure.Services;

public sealed class TeacherContractPdfWriter(IWebHostEnvironment env) : ITeacherContractPdfWriter
{
    static TeacherContractPdfWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
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
        var tutorsphereLogo = LoadBrandPng("tutorsphere-logo.png", "tutorsphere-logo-full.png");
        var gisebsLogo = LoadBrandPng("gisebs-logo.png");
        var groupLogo = LoadGroupLogo(model.GroupLogoUrl);
        var chrome = model.Chrome;
        var rtl = string.Equals(
            SupportedLanguageCodes.Normalize(model.Language),
            SupportedLanguageCodes.Arabic,
            StringComparison.OrdinalIgnoreCase);
        var fonts = FontsFor(model.Language);

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken3).FontFamily(fonts));
                if (rtl)
                    page.ContentFromRightToLeft();

                page.Header().Column(col =>
                {
                    col.Item().ContentFromLeftToRight().Row(row =>
                    {
                        row.Spacing(10);
                        if (tutorsphereLogo is { Length: > 0 })
                            row.ConstantItem(128).Height(42).Image(tutorsphereLogo).FitArea();
                        if (gisebsLogo is { Length: > 0 })
                            row.ConstantItem(118).Background("#0b0b12").Padding(4).Height(42).Image(gisebsLogo).FitArea();
                        if (groupLogo is { Length: > 0 })
                            row.ConstantItem(48).Height(42).Image(groupLogo).FitArea();
                        row.RelativeItem();
                        row.ConstantItem(64).Height(64).Image(qrPng);
                    });
                    col.Item().PaddingTop(8).Text(chrome.DocumentTitle).Bold().FontSize(13).FontColor("#5831E0");
                    col.Item().PaddingTop(2).Text(
                        $"N° {model.ContractNumber}  ·  {model.Version}  ·  {model.Language.ToUpperInvariant()}  ·  {model.GroupName}");
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#5831E0");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);
                    foreach (var (title, body) in model.Sections)
                    {
                        col.Item().Text(title).Bold().FontSize(11).FontColor("#5831E0");
                        col.Item().Text(StripMd(body)).FontSize(9.5f).LineHeight(1.35f);
                    }

                    col.Item().PaddingTop(12).Text(chrome.AcceptanceTitle).Bold().FontSize(12).FontColor("#5831E0");
                    col.Item().Text($"{chrome.ForGroup} : {model.GroupSignatoryName} — {model.GroupSignatoryRole}");
                    col.Item().Text($"{chrome.ForTeacher} : {model.TeacherName}");
                    col.Item().Text(chrome.ConsentMention);
                    col.Item().Text($"{chrome.SignedAtLabel} : {model.SignedAtUtc:yyyy-MM-dd HH:mm:ss}");
                    if (signature is { Length: > 0 })
                    {
                        col.Item().Text($"{chrome.TeacherSignatureLabel} :").Bold();
                        col.Item().ContentFromLeftToRight().Width(220).Height(80).Image(signature);
                    }
                    col.Item().PaddingTop(8).Text($"{chrome.VerificationRef} : {model.VerificationCode}").Bold();
                    col.Item().Text($"{chrome.HashLabel} : {model.DocumentHashPlaceholder}").FontSize(8);
                    col.Item().Text($"{chrome.VerificationUrlLabel} : {model.VerificationUrl}").FontSize(8);
                    col.Item().Text(chrome.ImmutableNotice).Italic().FontSize(8);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"{model.ContractNumber} · {chrome.PageLabel} ");
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

    private byte[]? LoadBrandPng(params string[] fileNames)
    {
        foreach (var name in fileNames)
        {
            foreach (var path in BrandCandidatePaths(name))
            {
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }
        }
        return null;
    }

    private IEnumerable<string> BrandCandidatePaths(string fileName)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "branding", fileName);
        yield return Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", fileName);
        yield return Path.Combine(env.ContentRootPath, "Assets", "branding", fileName);
        if (!string.IsNullOrWhiteSpace(env.WebRootPath))
            yield return Path.Combine(env.WebRootPath, "images", fileName);
        yield return Path.Combine(env.ContentRootPath, "wwwroot", "images", fileName);
    }

    private byte[]? LoadGroupLogo(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;
        var fileName = Path.GetFileName(logoUrl.Replace('\\', '/').Split('?', 2)[0]);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..", StringComparison.Ordinal))
            return null;

        var candidates = new[]
        {
            Path.Combine(env.ContentRootPath, "uploads", fileName),
            string.IsNullOrWhiteSpace(env.WebRootPath) ? "" : Path.Combine(env.WebRootPath, "uploads", fileName)
        };
        foreach (var path in candidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return File.ReadAllBytes(path);
        }
        return null;
    }

    private static string[] FontsFor(string language)
    {
        var lang = SupportedLanguageCodes.Normalize(language);
        if (lang == SupportedLanguageCodes.MandarinChinese)
        {
            return
            [
                "Microsoft YaHei", "Microsoft YaHei UI", "Noto Sans CJK SC", "Source Han Sans SC",
                "SimSun", "Segoe UI", "Arial Unicode MS", "DejaVu Sans"
            ];
        }

        if (lang == SupportedLanguageCodes.Arabic)
        {
            return
            [
                "Segoe UI", "Tahoma", "Arial", "Noto Naskh Arabic", "Noto Sans Arabic",
                "Traditional Arabic", "DejaVu Sans"
            ];
        }

        return ["Segoe UI", "Calibri", "Arial", "Noto Sans", "DejaVu Sans"];
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
