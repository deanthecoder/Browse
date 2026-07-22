// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.Concurrent;
using Browse.Models;

namespace Browse.Services;

/// <summary>
/// Enumerates and briefly caches folder contents away from the UI thread.
/// </summary>
/// <remarks>
/// Short-lived snapshots make backtracking instant without allowing stale data to linger.
/// </remarks>
public sealed class DirectoryContentService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, CacheEntry> m_cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<BrowserItem>> GetItemsAsync(
        DirectoryInfo directory,
        BrowserSettings settings,
        CancellationToken cancellationToken = default)
    {
        var path = directory.FullName;
        if (!m_cache.TryGetValue(path, out var cacheEntry) || DateTime.UtcNow - cacheEntry.CreatedAt > CacheLifetime)
        {
            var items = await Task.Run(() => Enumerate(directory, cancellationToken), cancellationToken);
            cacheEntry = new CacheEntry(DateTime.UtcNow, items);
            m_cache[path] = cacheEntry;
        }

        return cacheEntry.Items
            .Where(item => settings.ShowHiddenItems || !item.IsHidden)
            .Where(item => settings.ShowDotFolders || !item.IsDotFolder)
            .ToArray();
    }

    public void Invalidate(DirectoryInfo directory) => m_cache.TryRemove(directory.FullName, out _);

    private static IReadOnlyList<BrowserItem> Enumerate(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        var items = new List<BrowserItem>();
        try
        {
            foreach (var info in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                items.Add(new BrowserItem(info));
            }
        }
        catch (UnauthorizedAccessException)
        {
            return items;
        }
        catch (IOException)
        {
            return items;
        }

        return items
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private sealed record CacheEntry(DateTime CreatedAt, IReadOnlyList<BrowserItem> Items);
}
