// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Globalization;
using System.Text.Json;
using Avalonia;
using PdfLibCore;
using PdfLibCore.Enums;

namespace Browse.Services.Previews;

/// <summary>
/// Implements the isolated process entry point for native PDFium rendering.
/// </summary>
internal static class PdfPreviewWorker
{
    internal const string Command = "--pdf-preview-worker";

    public static bool TryRun(IReadOnlyList<string> args, out int exitCode)
    {
        exitCode = 0;
        if (args.Count == 0 || !args[0].Equals(Command, StringComparison.Ordinal))
            return false;

        if (args.Count != 6 ||
            !int.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out var maximumDimension) ||
            !int.TryParse(args[5], NumberStyles.None, CultureInfo.InvariantCulture, out var dpi) ||
            maximumDimension < 0 || dpi <= 0)
        {
            exitCode = 2;
            return true;
        }

        try
        {
            var result = Render(args[1], args[2], maximumDimension, dpi);
            File.WriteAllText(args[3], JsonSerializer.Serialize(result));
        }
        catch
        {
            // The parent intentionally treats every worker failure as unavailable PDF content.
            exitCode = 1;
        }
        return true;
    }

    internal static PdfRenderResult Render(string pdfPath, string bitmapPath, int maximumDimension, int dpi)
    {
        using var input = File.OpenRead(pdfPath);
        using var document = new PdfDocument(input);
        if (document.Pages.Count == 0)
            return new PdfRenderResult(0, 0, 0);

        using var page = document.Pages[0];
        var pageWidth = page.Size.Width;
        var pageHeight = page.Size.Height;
        if (!double.IsFinite(pageWidth) || !double.IsFinite(pageHeight) || pageWidth <= 0 || pageHeight <= 0)
            throw new InvalidDataException("The first PDF page has invalid dimensions.");

        var size = GetRenderSize(pageWidth, pageHeight, maximumDimension, dpi);
        var width = size.Width;
        var height = size.Height;
        using var rendered = new PdfiumBitmap(width, height, true);
        page.Render(rendered, PageOrientations.Normal, RenderingFlags.LcdText | RenderingFlags.Annotations);
        using var bitmapStream = rendered.AsBmpStream(dpi, dpi);
        using var output = File.Create(bitmapPath);
        bitmapStream.CopyTo(output);
        return new PdfRenderResult(document.Pages.Count, pageWidth, pageHeight);
    }

    internal static PixelSize GetRenderSize(double pageWidth, double pageHeight, int maximumDimension, int dpi)
    {
        var scale = maximumDimension > 0
            ? Math.Min((double)maximumDimension / pageWidth, (double)maximumDimension / pageHeight)
            : dpi / 72.0;
        var width = Math.Max(1, checked((int)Math.Round(pageWidth * scale)));
        var height = Math.Max(1, checked((int)Math.Round(pageHeight * scale)));
        if (maximumDimension > 0)
            return new PixelSize(Math.Min(width, maximumDimension), Math.Min(height, maximumDimension));
        const int maximumClipboardDimension = 16_384;
        if (width > maximumClipboardDimension || height > maximumClipboardDimension)
            throw new InvalidDataException("The first PDF page is too large to copy as an image.");
        return new PixelSize(width, height);
    }
}

internal sealed record PdfRenderResult(int PageCount, double PageWidth, double PageHeight);
