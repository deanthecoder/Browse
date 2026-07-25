// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Browse.Models;
using Browse.Services.Thumbnails;
using DTC.Core.Extensions;

namespace Browse.Services.Previews;

/// <summary>
/// Creates video thumbnails using Finder or Explorer's native preview service.
/// </summary>
/// <remarks>
/// Codec support follows the operating system, avoiding bundled media decoders and matching the native file browser.
/// </remarks>
public sealed class VideoPreviewProvider(PlatformThumbnailService thumbnailService = null) : IPreviewProvider
{
    private const int MaxPreviewDimension = 900;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avi", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".webm", ".wmv"
    };
    private readonly PlatformThumbnailService m_thumbnailService = thumbnailService ?? new PlatformThumbnailService();

    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(!item.IsDirectory && Extensions.Contains(item.EffectiveExtension));

    public async Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
    {
        var bitmap = await m_thumbnailService.CreateAsync((FileInfo)item.Info, MaxPreviewDimension, cancellationToken);
        return bitmap == null
            ? new EmptyPreviewContent(
                item.Name,
                item.FullPath,
                $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g} · No system thumbnail available")
            : new ImagePreviewContent(
                item.Name,
                item.FullPath,
                $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g} · Video thumbnail",
                bitmap);
    }
}
