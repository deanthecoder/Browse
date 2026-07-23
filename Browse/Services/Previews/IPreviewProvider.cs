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

namespace Browse.Services.Previews;

/// <summary>
/// Detects and loads one family of file-system previews.
/// </summary>
/// <remarks>
/// Providers return bounded data models and never construct UI controls.
/// </remarks>
public interface IPreviewProvider
{
    /// <summary>
    /// Determines asynchronously whether this provider supports an item.
    /// </summary>
    ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Creates bounded preview content for a supported item.
    /// </summary>
    Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken);
}
