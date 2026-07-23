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
using Browse.Services.Previews;

namespace Browse.Services;

/// <summary>
/// Produces fast, bounded previews for selected file-system entries.
/// </summary>
/// <remarks>
/// File reads are limited so selection changes remain cheap even for very large files.
/// </remarks>
public sealed class PreviewService
{
    private readonly IReadOnlyList<IPreviewProvider> m_providers;

    public PreviewService(IEnumerable<IPreviewProvider> providers = null)
    {
        m_providers = providers?.ToArray() ??
        [
            new FolderPreviewProvider(),
            new ImagePreviewProvider(),
            new ArchivePreviewProvider(),
            new TextPreviewProvider(),
            new FallbackPreviewProvider()
        ];
    }

    public async Task<PreviewContent> CreateAsync(IReadOnlyList<BrowserItem> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return new EmptyPreviewContent();
        if (items.Count > 1)
        {
            var fileCount = items.Count(item => !item.IsDirectory);
            var folderCount = items.Count - fileCount;
            return new MultiplePreviewContent(
                $"{items.Count:N0} selected",
                $"{folderCount:N0} folders · {fileCount:N0} files");
        }

        var item = items[0];
        foreach (var provider in m_providers)
        {
            if (await provider.CanPreviewAsync(item, cancellationToken))
                return await provider.CreateAsync(item, cancellationToken);
        }
        return new EmptyPreviewContent(item.Name, item.FullPath);
    }
}
