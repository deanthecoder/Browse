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
/// Retrieves a thumbnail through one desktop platform's native file-preview service.
/// </summary>
/// <remarks>
/// Implementations return null when the operating system has no thumbnail for the file.
/// </remarks>
public interface IPlatformThumbnailProvider
{
    Task<Bitmap> CreateAsync(FileInfo file, int maximumDimension, CancellationToken cancellationToken);
}
