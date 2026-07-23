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
using System.Text;
using Browse.Models;
using DTC.Core.Extensions;

namespace Browse.Services;

/// <summary>
/// Produces fast, bounded previews for selected file-system entries.
/// </summary>
/// <remarks>
/// File reads are limited so selection changes remain cheap even for very large files.
/// </remarks>
public sealed class PreviewService
{
    private const int MaxPreviewBytes = 8 * 1024;
    private const int MaxPreviewLines = 600;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
    };
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".c", ".cmd", ".cpp", ".cs", ".css", ".h", ".hpp", ".htm", ".html",
        ".js", ".json", ".jsx", ".ps1", ".py", ".sh", ".ts", ".tsx", ".xml", ".xaml", ".axaml"
    };

    public async Task<PreviewResult> CreateAsync(IReadOnlyList<BrowserItem> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return new PreviewResult(PreviewKind.None, "No selection");
        if (items.Count > 1)
        {
            var fileCount = items.Count(item => !item.IsDirectory);
            var folderCount = items.Count - fileCount;
            return new PreviewResult(
                PreviewKind.Multiple,
                $"{items.Count:N0} selected",
                Details: $"{folderCount:N0} folders · {fileCount:N0} files");
        }

        var item = items[0];
        if (item.IsDirectory)
            return new PreviewResult(PreviewKind.Folder, item.Name, item.FullPath, $"Folder · Modified {item.LastWriteTime:g}");

        var file = (FileInfo)item.Info;
        var size = item.Size?.ToSize() ?? "Unknown size";
        var details = $"{size} · Modified {item.LastWriteTime:g}";
        if (ImageExtensions.Contains(file.Extension))
            return new PreviewResult(PreviewKind.Image, item.Name, item.FullPath, details, ImagePath: file.FullName);
        if (file.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return await CreateZipPreviewAsync(item, file, details, cancellationToken);

        bool isText;
        try
        {
            isText = await Task.Run(file.IsTextFile, cancellationToken);
        }
        catch (IOException)
        {
            isText = false;
        }
        if (!isText)
            return new PreviewResult(PreviewKind.None, item.Name, item.FullPath, details);

        var content = await ReadTextSampleAsync(file, cancellationToken);
        var kind = file.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            ? PreviewKind.Markdown
            : CodeExtensions.Contains(file.Extension) ? PreviewKind.Code : PreviewKind.Text;
        return new PreviewResult(kind, item.Name, item.FullPath, details, content);
    }

    private static Task<PreviewResult> CreateZipPreviewAsync(
        BrowserItem item,
        FileInfo file,
        string details,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            using var archive = ZipFile.OpenRead(file.FullName);
            const int maxEntries = 200;
            var entries = archive.Entries.Take(maxEntries).ToArray();
            var content = string.Join('\n', entries.Select(entry => $"{entry.FullName}\t{entry.Length:N0} bytes"));
            if (archive.Entries.Count > maxEntries)
                content += $"\n\n… {archive.Entries.Count - maxEntries:N0} more entries …";
            return new PreviewResult(
                PreviewKind.Archive,
                item.Name,
                item.FullPath,
                $"{details} · {archive.Entries.Count:N0} entries",
                content);
        }
        catch (InvalidDataException)
        {
            return new PreviewResult(PreviewKind.None, item.Name, item.FullPath, $"{details} · Invalid ZIP archive");
        }
    }, cancellationToken);

    private static async Task<string> ReadTextSampleAsync(FileInfo file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenRead();
        var byteCount = (int)Math.Min(file.Length, MaxPreviewBytes);
        var buffer = new byte[byteCount];
        var read = await stream.ReadAsync(buffer.AsMemory(0, byteCount), cancellationToken);
        var content = Encoding.UTF8.GetString(buffer, 0, read);
        var lines = content.Split('\n');
        var wasTruncated = file.Length > MaxPreviewBytes || lines.Length > MaxPreviewLines;
        if (lines.Length > MaxPreviewLines)
            content = string.Join('\n', lines.Take(MaxPreviewLines));
        return wasTruncated ? content + "\n\n… preview truncated …" : content;
    }
}
