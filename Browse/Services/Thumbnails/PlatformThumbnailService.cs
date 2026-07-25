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

namespace Browse.Services.Thumbnails;

/// <summary>
/// Selects the native thumbnail implementation for the current desktop platform.
/// </summary>
/// <remarks>
/// Keeping platform selection outside preview providers allows other file types to reuse native thumbnails later.
/// </remarks>
public sealed class PlatformThumbnailService
{
    private readonly IPlatformThumbnailProvider m_provider;

    public PlatformThumbnailService(IPlatformThumbnailProvider provider = null)
    {
        m_provider = provider ?? CreateProvider();
    }

    public Task<Bitmap> CreateAsync(FileInfo file, int maximumDimension, CancellationToken cancellationToken) =>
        m_provider?.CreateAsync(file, maximumDimension, cancellationToken) ?? Task.FromResult<Bitmap>(null);

    private static IPlatformThumbnailProvider CreateProvider()
    {
        if (OperatingSystem.IsMacOS())
            return new MacQuickLookThumbnailProvider();
        if (OperatingSystem.IsWindows())
            return new WindowsShellThumbnailProvider();
        return null;
    }
}
