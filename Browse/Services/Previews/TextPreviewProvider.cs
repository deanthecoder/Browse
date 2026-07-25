// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Text;
using Browse.Models;
using DTC.Core.Extensions;

namespace Browse.Services.Previews;

/// <summary>
/// Creates bounded previews for plain text, source code, and Markdown files.
/// </summary>
/// <remarks>
/// Detection samples unknown extensions while common source extensions are recognized immediately.
/// </remarks>
public sealed class TextPreviewProvider : IPreviewProvider
{
    private const int MaxPreviewBytes = 8 * 1024;
    private const int MaxPreviewLines = 600;
    public async ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken)
    {
        if (item.IsDirectory)
            return false;
        try
        {
            return await Task.Run(((FileInfo)item.Info).IsTextFile, cancellationToken);
        }
        catch (IOException)
        {
            return false;
        }
    }

    public async Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
    {
        var file = (FileInfo)item.Info;
        var sample = await ReadSampleAsync(file, cancellationToken);
        var language = TextMateLanguageResolver.Resolve($"file{item.EffectiveExtension}");
        var mode = GetPreviewMode(item.EffectiveExtension, language);
        return new TextPreviewContent(
            item.Name,
            item.FullPath,
            $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g}",
            sample.Text,
            mode,
            language,
            sample.IsTruncated);
    }

    private static TextPreviewMode GetPreviewMode(string extension, string language)
    {
        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
            return TextPreviewMode.Markdown;
        if (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
            return TextPreviewMode.Html;
        return language == null ? TextPreviewMode.Plain : TextPreviewMode.Code;
    }

    private static async Task<TextSample> ReadSampleAsync(FileInfo file, CancellationToken cancellationToken)
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
        return new TextSample(wasTruncated ? content + "\n\n… preview truncated …" : content, wasTruncated);
    }

    private sealed record TextSample(string Text, bool IsTruncated);
}
