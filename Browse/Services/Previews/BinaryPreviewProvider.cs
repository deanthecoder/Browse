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
/// Creates bounded hexadecimal previews for otherwise unsupported binary files.
/// </summary>
/// <remarks>
/// A conventional offset, byte, and printable-character layout makes unknown formats inspectable without loading them in full.
/// </remarks>
public sealed class BinaryPreviewProvider : IPreviewProvider
{
    private const int BytesPerLine = 16;
    private const int MaxMiniPreviewBytes = 512;
    internal const int MaxExpandedPreviewBytes = 64 * 1024;

    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(!item.IsDirectory);

    public async Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
    {
        var file = (FileInfo)item.Info;
        return new TextPreviewContent(
            item.Name,
            item.FullPath,
            $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g} · Binary",
            await ReadHexDumpAsync(file, MaxMiniPreviewBytes, cancellationToken),
            TextPreviewMode.Hex,
            isTruncated: file.Length > MaxMiniPreviewBytes);
    }

    internal static async Task<string> ReadHexDumpAsync(
        FileInfo file,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var requestedBytes = (int)Math.Min(stream.Length, maximumBytes);
        var buffer = new byte[requestedBytes];
        var bytesRead = 0;
        while (bytesRead < requestedBytes)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(bytesRead, requestedBytes - bytesRead), cancellationToken);
            if (count == 0)
                break;
            bytesRead += count;
        }

        var text = FormatHexDump(buffer.AsSpan(0, bytesRead));
        return stream.Length > bytesRead ? text + "\n… hex preview truncated …" : text;
    }

    internal static string FormatHexDump(ReadOnlySpan<byte> bytes)
    {
        var output = new StringBuilder();
        for (var offset = 0; offset < bytes.Length; offset += BytesPerLine)
        {
            if (output.Length > 0)
                output.AppendLine();
            var count = Math.Min(BytesPerLine, bytes.Length - offset);
            output.Append($"{offset:X8}  ");
            for (var index = 0; index < BytesPerLine; index++)
            {
                if (index == BytesPerLine / 2)
                    output.Append(' ');
                output.Append(index < count ? $"{bytes[offset + index]:X2} " : "   ");
            }
            output.Append(" |");
            for (var index = 0; index < count; index++)
            {
                var value = bytes[offset + index];
                output.Append(value is >= 0x20 and <= 0x7e ? (char)value : '.');
            }
            output.Append('|');
        }
        return output.ToString();
    }
}
