using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using MigraDoc.Rendering;
using SkiaSharp;

namespace PDFProofer.Core.Services;

/// <summary>
/// PDF processing service for proof generation, thumbnails, and optimization (FR-PP-003 through FR-PP-007)
/// </summary>
public class PdfProcessor
{
    private readonly int _proofDpi = 150;
    private readonly int _thumbnailDpi = 72;

    /// <summary>
    /// Generate proof PDF with a semi-transparent diagonal "PROOF" watermark on every page.
    /// </summary>
    public string GenerateProofPdf(string inputPath, string outputPath)
    {
        // Open source PDF for import
        using var input = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        var output = new PdfDocument();
        output.Version = input.Version;

        foreach (var page in input.Pages)
        {
            // Add a copy of the page to the output document
            var newPage = output.AddPage(page);

            // Draw watermark
            using var gfx = XGraphics.FromPdfPage(newPage, XGraphicsPdfPageOptions.Prepend);

            // Prepare large, semi-transparent font
            var fontSize = Math.Min(newPage.Width.Point, newPage.Height.Point) / 4;
            // Use regular style; PDFsharp 6 exposes different style constants than older versions
            var font = new XFont("Arial", fontSize, XFontStyle.Regular);
            var color = XColor.FromArgb(60, XColors.Red);
            var brush = new XSolidBrush(color);

            // Centered rectangle for text placement
            var rect = new XRect(0, 0, newPage.Width, newPage.Height);

            // Rotate and draw centered diagonal text
            gfx.Save();
            gfx.TranslateTransform(newPage.Width / 2, newPage.Height / 2);
            gfx.RotateTransform(-45);
            gfx.TranslateTransform(-newPage.Width / 2, -newPage.Height / 2);

            var format = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            gfx.DrawString("PROOF", font, brush, new XRect(0, 0, newPage.Width, newPage.Height), format);
            gfx.Restore();
        }

        // Save output
        output.Save(outputPath);
        return outputPath;
    }

    /// <summary>
    /// Generate a thumbnail image for the first page. Uses a fast placeholder rasterization by
    /// rendering a lightweight representation with the job filename and page size. For full-fidelity
    /// rasterization replace this with a PDF renderer (Pdfium, Ghostscript, or MigraDoc rasterizer).
    /// </summary>
    public string GenerateThumbnail(string inputPath, string outputPath)
    {
        // Attempt to open PDF to read page size
        using var pdf = PdfReader.Open(inputPath, PdfDocumentOpenMode.ReadOnly);
        var page = pdf.Pages.Count > 0 ? pdf.Pages[0] : null;

        // Fallback sizes (points -> pixels at thumbnail DPI)
        var widthPts = page?.Width.Point ?? 612; // 8.5in * 72
        var heightPts = page?.Height.Point ?? 792; // 11in * 72

        var scale = _thumbnailDpi / 72.0; // convert points (72 DPI) to target DPI
        var widthPx = Math.Max(1, (int)Math.Round(widthPts * scale));
        var heightPx = Math.Max(1, (int)Math.Round(heightPts * scale));

        // Try Ghostscript (preferred) to render the first page to PNG, fall back to Skia placeholder
        if (GhostscriptHelper.IsAvailable())
        {
            try
            {
                var ok = GhostscriptHelper.RenderPdfPageToPng(inputPath, 0, _thumbnailDpi, outputPath);
                if (ok) return outputPath;
            }
            catch
            {
                // ignore and fall back
            }
        }

        // Create a simple thumbnail with SkiaSharp: white background, filename text, small watermark.
        using var surface = SKSurface.Create(new SKImageInfo(widthPx, heightPx, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Draw a faint border
        using (var paint = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 2 })
        {
            canvas.DrawRect(1, 1, widthPx - 2, heightPx - 2, paint);
        }

        // Draw filename in center
        var filename = Path.GetFileNameWithoutExtension(inputPath) ?? "document";
        using (var paint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = Math.Max(12, Math.Min(widthPx, heightPx) / 12),
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        })
        {
            canvas.DrawText(filename, widthPx / 2, heightPx / 2, paint);
        }

        // Small PROOF overlay in corner for proof thumbnails
        using (var paint = new SKPaint
        {
            Color = new SKColor(255, 0, 0, 100),
            TextSize = Math.Max(10, Math.Min(widthPx, heightPx) / 20),
            IsAntialias = true,
            TextAlign = SKTextAlign.Right
        })
        {
            canvas.DrawText("PROOF", widthPx - 8, heightPx - 8, paint);
        }

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 85);
        using var fs = File.OpenWrite(outputPath);
        data.SaveTo(fs);

        return outputPath;
    }

    /// <summary>
    /// Optimize PDF for printing (FR-PP-007) — stub, copies input.
    /// </summary>
    public string OptimizePdf(string inputPath, string outputPath, bool useGhostscript = false)
    {
        if (useGhostscript && GhostscriptHelper.IsAvailable())
        {
            var ok = GhostscriptHelper.OptimizePdfWithGhostscript(inputPath, outputPath);
            if (ok) return outputPath;
            // fall back if ghostscript failed
        }

        // Default: copy file
        File.Copy(inputPath, outputPath, true);
        return outputPath;
    }
}
