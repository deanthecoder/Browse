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
using DTC.Core.Extensions;

namespace Browse.Services.Previews;

/// <summary>
/// Supplies metadata when no richer preview provider matches.
/// </summary>
/// <remarks>
/// This provider is deliberately registered last as the universal fallback.
/// </remarks>
public sealed class FallbackPreviewProvider : IPreviewProvider
{
    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);

    public Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken) =>
        Task.FromResult<PreviewContent>(new EmptyPreviewContent(
            item.Name,
            item.FullPath,
            $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g}"));
}
