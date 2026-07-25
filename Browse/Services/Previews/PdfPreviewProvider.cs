// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Media.Imaging;
using Browse.Models;
using DTC.Core.Extensions;
using PdfLibCore;
using PdfLibCore.Enums;

namespace Browse.Services.Previews;

/// <summary>
/// Renders the first page of a PDF into a bounded bitmap preview.
/// </summary>
/// <remarks>
/// PDFium work runs off the UI thread and output dimensions are capped to keep selection responsive.
/// </remarks>
public sealed class PdfPreviewProvider : IPreviewProvider
{
    private const int MaxPreviewDimension = 900;

    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            !item.IsDirectory && item.EffectiveExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase));

    public Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken) => Task.Run<PreviewContent>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = (FileInfo)item.Info;
        using var input = file.OpenRead();
        using var document = new PdfDocument(input);
        if (document.Pages.Count == 0)
            return new EmptyPreviewContent(item.Name, item.FullPath, "The PDF contains no pages.");

        using var page = document.Pages[0];
        var scale = Math.Min(
            (double)MaxPreviewDimension / page.Size.Width,
            (double)MaxPreviewDimension / page.Size.Height);
        var width = Math.Max(1, (int)Math.Round(page.Size.Width * scale));
        var height = Math.Max(1, (int)Math.Round(page.Size.Height * scale));
        using var rendered = new PdfiumBitmap(width, height, true);
        page.Render(rendered, PageOrientations.Normal, RenderingFlags.LcdText | RenderingFlags.Annotations);
        using var bitmapStream = rendered.AsBmpStream(96, 96);
        var bitmap = new Bitmap(bitmapStream);
        cancellationToken.ThrowIfCancellationRequested();

        var details = $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g}\n" +
                      $"{document.Pages.Count:N0} page{(document.Pages.Count == 1 ? string.Empty : "s")} · " +
                      $"First page {page.Size.Width:N0} × {page.Size.Height:N0} pt";
        return new ImagePreviewContent(item.Name, item.FullPath, details, bitmap);
    }, cancellationToken);
}
