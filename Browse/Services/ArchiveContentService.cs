// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.IO.Compression;
using Browse.Models;

namespace Browse.Services;

/// <summary>Enumerates and extracts the read-only, virtual contents of ZIP archives.</summary>
public sealed class ArchiveContentService : IDisposable
{
    private readonly DirectoryInfo m_dragStagingDirectory = new(Path.Combine(Path.GetTempPath(), "Browse", Guid.NewGuid().ToString("N")));

    public Task<IReadOnlyList<BrowserItem>> GetItemsAsync(FileInfo archive, string folderPath, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<BrowserItem>>(() =>
        {
            using var zip = ZipFile.OpenRead(archive.FullName);
            var prefix = NormalizeFolderPath(folderPath);
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<BrowserItem>();
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = NormalizeEntryPath(entry.FullName);
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || path.Length == prefix.Length)
                    continue;
                var remaining = path[prefix.Length..];
                var separator = remaining.IndexOf('/');
                if (separator >= 0)
                {
                    folders.Add(remaining[..separator]);
                    continue;
                }
                if (!string.IsNullOrEmpty(entry.Name))
                    files.Add(BrowserItem.FromArchiveEntry(archive, entry, prefix + remaining, false));
            }
            var folderItems = folders.Select(name => BrowserItem.FromArchiveDirectory(archive, prefix + name + "/"));
            return folderItems.Concat(files)
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }, cancellationToken);

    public Task ExtractAsync(IEnumerable<BrowserItem> items, DirectoryInfo destination, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            destination.Create();
            foreach (var group in items.Where(item => item.IsArchiveEntry).GroupBy(item => item.ArchivePath, StringComparer.OrdinalIgnoreCase))
            {
                using var zip = ZipFile.OpenRead(group.Key);
                foreach (var item in group)
                    ExtractItem(zip, item, destination, cancellationToken);
            }
        }, cancellationToken);

    public async Task<IReadOnlyList<string>> CreateDragFilesAsync(IEnumerable<BrowserItem> items, CancellationToken cancellationToken = default)
    {
        m_dragStagingDirectory.Create();
        var staging = new DirectoryInfo(Path.Combine(m_dragStagingDirectory.FullName, Guid.NewGuid().ToString("N")));
        await ExtractAsync(items, staging, cancellationToken);
        return staging.EnumerateFileSystemInfos().Select(item => item.FullName).ToArray();
    }

    private static void ExtractItem(ZipArchive zip, BrowserItem item, DirectoryInfo destination, CancellationToken cancellationToken)
    {
        var prefix = NormalizeFolderPath(item.ArchiveEntryPath);
        var entries = item.IsDirectory
            ? zip.Entries.Where(entry => NormalizeEntryPath(entry.FullName).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            : zip.Entries.Where(entry => string.Equals(NormalizeEntryPath(entry.FullName), prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = item.IsDirectory
                ? NormalizeEntryPath(entry.FullName)[prefix.Length..]
                : item.Name;
            if (string.IsNullOrEmpty(relativePath))
                continue;
            var outputPath = GetSafeOutputPath(destination, Path.Combine(item.IsDirectory ? item.Name : string.Empty, relativePath));
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }
            var output = new FileInfo(FileOperationService.GetAvailablePath(outputPath));
            output.Directory?.Create();
            using var source = entry.Open();
            using var target = new FileStream(output.FullName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
        }
    }

    private static string GetSafeOutputPath(DirectoryInfo destination, string relativePath)
    {
        var root = Path.GetFullPath(destination.FullName) + Path.DirectorySeparatorChar;
        var output = Path.GetFullPath(Path.Combine(destination.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!output.StartsWith(root, OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The archive contains an unsafe path.");
        return output;
    }

    private static string NormalizeFolderPath(string path) => string.IsNullOrWhiteSpace(path) ? string.Empty : NormalizeEntryPath(path).TrimEnd('/') + "/";
    private static string NormalizeEntryPath(string path) => path.Replace('\\', '/').TrimStart('/');

    public void Dispose()
    {
        try
        {
            if (m_dragStagingDirectory.Exists)
                m_dragStagingDirectory.Delete(true);
        }
        catch
        {
            // The OS will clean any locked staging files after Browse exits.
        }
    }
}
