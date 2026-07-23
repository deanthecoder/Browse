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
/// Creates inexpensive folder metadata previews.
/// </summary>
/// <remarks>
/// Recursive size calculation remains a separate user-triggered operation.
/// </remarks>
public sealed class FolderPreviewProvider : IPreviewProvider
{
    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(item.IsDirectory);

    public Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken) =>
        Task.FromResult<PreviewContent>(new FolderPreviewContent(
            item.Name,
            item.FullPath,
            $"Folder · Modified {item.LastWriteTime:g}"));
}
