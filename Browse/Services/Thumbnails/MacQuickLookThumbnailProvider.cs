// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Diagnostics;
using Avalonia.Media.Imaging;

namespace Browse.Services.Thumbnails;

/// <summary>
/// Requests file thumbnails from macOS Quick Look.
/// </summary>
/// <remarks>
/// The system helper writes into an isolated temporary directory that is removed immediately after decoding.
/// </remarks>
public sealed class MacQuickLookThumbnailProvider : IPlatformThumbnailProvider
{
    public async Task<Bitmap> CreateAsync(FileInfo file, int maximumDimension, CancellationToken cancellationToken)
    {
        var outputDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"Browse-thumbnail-{Guid.NewGuid():N}"));
        outputDirectory.Create();
        Process process = null;
        try
        {
            var startInfo = new ProcessStartInfo("/usr/bin/qlmanage")
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("-s");
            startInfo.ArgumentList.Add(maximumDimension.ToString());
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputDirectory.FullName);
            startInfo.ArgumentList.Add(file.FullName);
            process = Process.Start(startInfo);
            if (process == null)
                return null;
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
                return null;
            var thumbnail = outputDirectory.EnumerateFiles("*.png").FirstOrDefault();
            if (thumbnail == null)
                return null;
            await using var stream = thumbnail.OpenRead();
            return new Bitmap(stream);
        }
        catch (OperationCanceledException)
        {
            if (process is { HasExited: false })
                process.Kill(true);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            process?.Dispose();
            try
            {
                if (outputDirectory.Exists)
                    outputDirectory.Delete(true);
            }
            catch (IOException)
            {
            }
        }
    }
}
