// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.IO.Compression;
using Browse.Models;
using DTC.Core.Extensions;

namespace Browse.Services.Previews;

/// <summary>
/// Lists a bounded number of entries from ZIP archives.
/// </summary>
/// <remarks>
/// Archive extraction is intentionally outside preview scope.
/// </remarks>
public sealed class ArchivePreviewProvider : IPreviewProvider
{
    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            !item.IsDirectory && Path.GetExtension(item.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase));

    public Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken) => Task.Run<PreviewContent>(() =>
    {
        var file = (FileInfo)item.Info;
        var details = $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g}";
        try
        {
            using var archive = ZipFile.OpenRead(file.FullName);
            const int maxEntries = 200;
            var entries = archive.Entries.Take(maxEntries).ToArray();
            var text = string.Join('\n', entries.Select(entry => $"{entry.FullName}\t{entry.Length:N0} bytes"));
            if (archive.Entries.Count > maxEntries)
                text += $"\n\n… {archive.Entries.Count - maxEntries:N0} more entries …";
            return new ArchivePreviewContent(
                item.Name,
                item.FullPath,
                $"{details} · {archive.Entries.Count:N0} entries",
                text);
        }
        catch (InvalidDataException)
        {
            return new EmptyPreviewContent(item.Name, item.FullPath, $"{details} · Invalid ZIP archive");
        }
    }, cancellationToken);
}
