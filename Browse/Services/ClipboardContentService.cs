// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Browse.Models;
using Browse.Services.Previews;
using DTC.Core.Extensions;

namespace Browse.Services;

internal enum ClipboardContentKind
{
    Text,
    Image,
    PdfFirstPage
}

/// <summary>
/// Copies a file's useful content representation to the system clipboard.
/// </summary>
/// <remarks>
/// Text remains text, known raster images are decoded, and PDFs provide a 72-DPI image of their first page.
/// </remarks>
public sealed class ClipboardContentService : IDisposable
{
    private const int PdfClipboardDpi = 72;
    private Bitmap m_retainedBitmap;

    public static bool CanCopy(BrowserItem item) => GetContentKind(item) != null;

    internal static ClipboardContentKind? GetContentKind(BrowserItem item)
    {
        if (item is not { IsDirectory: false, IsArchiveEntry: false, Info: FileInfo file } || !file.Exists)
            return null;
        if (item.EffectiveExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            return ClipboardContentKind.Text;
        if (item.EffectiveExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return ClipboardContentKind.PdfFirstPage;
        if (ImagePreviewProvider.Extensions.Contains(item.EffectiveExtension))
            return ClipboardContentKind.Image;
        try
        {
            return file.IsTextFile() ? ClipboardContentKind.Text : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task CopyAsync(IClipboard clipboard, BrowserItem item, CancellationToken cancellationToken = default)
    {
        if (clipboard == null)
            throw new InvalidOperationException("The clipboard is unavailable.");
        var kind = GetContentKind(item);
        if (kind == null || item.Info is not FileInfo file)
            throw new InvalidOperationException("Select one supported file to copy its content.");

        if (kind == ClipboardContentKind.Text)
        {
            var content = await File.ReadAllTextAsync(file.FullName, cancellationToken);
            await clipboard.SetTextAsync(content);
            var previous = m_retainedBitmap;
            m_retainedBitmap = null;
            previous?.Dispose();
            return;
        }

        var bitmap = kind == ClipboardContentKind.Image
            ? await Task.Run(
                () => ImagePreviewProvider.DecodeFullSize(file, item.EffectiveExtension),
                cancellationToken)
            : (await PdfPreviewProvider.RenderFirstPageAsync(file, 0, PdfClipboardDpi, cancellationToken)).Bitmap ??
              throw new InvalidOperationException("The PDF contains no pages.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await clipboard.SetBitmapAsync(bitmap);
            if (OperatingSystem.IsWindows())
            {
                await clipboard.FlushAsync();
                bitmap.Dispose();
            }
            else
            {
                var previous = m_retainedBitmap;
                m_retainedBitmap = bitmap;
                previous?.Dispose();
            }
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        m_retainedBitmap?.Dispose();
        m_retainedBitmap = null;
    }
}
