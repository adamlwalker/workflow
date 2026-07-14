using System.Diagnostics;

namespace PDFProofer.Core.Services;

public static class GhostscriptHelper
{
    // Check whether `gs` (Ghostscript) is available on PATH
    public static bool IsAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "gs",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return false;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    public static bool OptimizePdfWithGhostscript(string inputPath, string outputPath)
    {
        try
        {
            var args = $"-q -dNOPAUSE -dBATCH -sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS=/prepress -sOutputFile=\"{outputPath}\" \"{inputPath}\"";
            var psi = new ProcessStartInfo
            {
                FileName = "gs",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return false;
            var stderr = p.StandardError.ReadToEnd();
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Render a single PDF page to a PNG using Ghostscript if available.
    /// </summary>
    public static bool RenderPdfPageToPng(string inputPath, int pageIndex, int dpi, string outputPath)
    {
        try
        {
            // Ghostscript pages are 1-based
            var page = pageIndex + 1;
            var args = $"-q -dNOPAUSE -dBATCH -sDEVICE=pngalpha -r{dpi} -dTextAlphaBits=4 -dGraphicsAlphaBits=4 -dFirstPage={page} -dLastPage={page} -sOutputFile=\"{outputPath}\" \"{inputPath}\"";
            var psi = new ProcessStartInfo
            {
                FileName = "gs",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return false;
            var stderr = p.StandardError.ReadToEnd();
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30000);
            return p.ExitCode == 0 && File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
    }
}
