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

namespace Browse.Services;

/// <summary>
/// Copies decoded image files to the system clipboard while bounding retained native bitmap memory.
/// </summary>
/// <remarks>
/// Windows can flush clipboard data into the OS immediately. Other platforms may request it lazily, so only the
/// latest bitmap is retained and the previous bitmap is disposed deterministically.
/// </remarks>
public sealed class ClipboardImageService : IDisposable
{
    private Bitmap m_retainedBitmap;

    public static bool CanCopy(BrowserItem item) =>
        !item.IsDirectory && ImagePreviewProvider.Extensions.Contains(item.EffectiveExtension);

    public async Task CopyAsync(IClipboard clipboard, BrowserItem item, CancellationToken cancellationToken = default)
    {
        if (clipboard == null)
            throw new InvalidOperationException("The clipboard is unavailable.");
        if (!CanCopy(item))
            throw new InvalidOperationException("Select one supported image to copy.");

        var bitmap = await Task.Run(
            () => ImagePreviewProvider.DecodeFullSize((FileInfo)item.Info, item.EffectiveExtension),
            cancellationToken);
        try
        {
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
