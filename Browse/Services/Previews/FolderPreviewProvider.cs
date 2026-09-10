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
/// Flat folder sizes are calculated lazily; recursive sizes remain a separate user-triggered operation.
/// </remarks>
public sealed class FolderPreviewProvider : IPreviewProvider
{
    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(item.IsDirectory);

    public async Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
    {
        var statistics = await Task.Run(
            () => TryCalculateFlatFolderStatistics(new DirectoryInfo(item.FullPath), cancellationToken),
            cancellationToken);
        return new FolderPreviewContent(
            item.Name,
            item.FullPath,
            $"Folder · Modified {item.LastWriteTime:g}",
            statistics?.Size,
            statistics?.FileCount,
            statistics?.FolderCount);
    }

    private static FolderStatistics? TryCalculateFlatFolderStatistics(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        try
        {
            long size = 0;
            long fileCount = 0;
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry is DirectoryInfo)
                    return null;
                size = checked(size + ((FileInfo)entry).Length);
                fileCount++;
            }
            return new FolderStatistics(size, fileCount, 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
