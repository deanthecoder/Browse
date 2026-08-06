// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Text.Json;
using PdfLibCore;
using PdfLibCore.Enums;

namespace Browse.Services.Previews;

/// <summary>
/// Implements the isolated process entry point for native PDFium rendering.
/// </summary>
internal static class PdfPreviewWorker
{
    internal const string Command = "--pdf-preview-worker";
    private const int MaxPreviewDimension = 900;

    public static bool TryRun(IReadOnlyList<string> args, out int exitCode)
    {
        exitCode = 0;
        if (args.Count == 0 || !args[0].Equals(Command, StringComparison.Ordinal))
            return false;

        if (args.Count != 4)
        {
            exitCode = 2;
            return true;
        }

        try
        {
            var result = Render(args[1], args[2]);
            File.WriteAllText(args[3], JsonSerializer.Serialize(result));
        }
        catch
        {
            // The parent intentionally treats every worker failure as an unavailable preview.
            exitCode = 1;
        }
        return true;
    }

    internal static PdfRenderResult Render(string pdfPath, string bitmapPath)
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

        var scale = Math.Min(MaxPreviewDimension / pageWidth, MaxPreviewDimension / pageHeight);
        var width = Math.Clamp((int)Math.Round(pageWidth * scale), 1, MaxPreviewDimension);
        var height = Math.Clamp((int)Math.Round(pageHeight * scale), 1, MaxPreviewDimension);
        using var rendered = new PdfiumBitmap(width, height, true);
        page.Render(rendered, PageOrientations.Normal, RenderingFlags.LcdText | RenderingFlags.Annotations);
        using var bitmapStream = rendered.AsBmpStream(96, 96);
        using var output = File.Create(bitmapPath);
        bitmapStream.CopyTo(output);
        return new PdfRenderResult(document.Pages.Count, pageWidth, pageHeight);
    }
}

internal sealed record PdfRenderResult(int PageCount, double PageWidth, double PageHeight);
