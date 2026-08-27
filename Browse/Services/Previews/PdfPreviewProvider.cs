// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Browse.Models;
using DTC.Core.Extensions;

namespace Browse.Services.Previews;

/// <summary>
/// Renders the first page of a PDF into a bounded bitmap preview.
/// </summary>
/// <remarks>
/// PDFium runs in a child process because native access violations cannot be safely caught in-process.
/// </remarks>
public sealed class PdfPreviewProvider : IPreviewProvider
{
    private const int MaxPreviewDimension = 900;
    private static readonly SemaphoreSlim s_renderLock = new(1, 1);
    private static readonly TimeSpan s_renderTimeout = TimeSpan.FromSeconds(20);

    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            !item.IsDirectory && item.EffectiveExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase));

    public async Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
    {
        var file = (FileInfo)item.Info;
        try
        {
            var rendered = await RenderFirstPageAsync(file, MaxPreviewDimension, 96, cancellationToken);
            if (rendered.Result.PageCount == 0)
                return new EmptyPreviewContent(item.Name, item.FullPath, "The PDF contains no pages.");

            var details = $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g}\n" +
                          $"{rendered.Result.PageCount:N0} page{(rendered.Result.PageCount == 1 ? string.Empty : "s")} · " +
                          $"First page {rendered.Result.PageWidth:N0} × {rendered.Result.PageHeight:N0} pt";
            return new ImagePreviewContent(item.Name, item.FullPath, details, rendered.Bitmap);
        }
        catch (PdfRenderException ex)
        {
            return Unavailable(item, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or
                                   JsonException or Win32Exception or ArgumentException or NotSupportedException)
        {
            return Unavailable(item, "The PDF preview could not be created.");
        }
    }

    internal static async Task<RenderedPdfPage> RenderFirstPageAsync(
        FileInfo file,
        int maximumDimension,
        int dpi,
        CancellationToken cancellationToken)
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), "Browse", "PdfPreview", Guid.NewGuid().ToString("N"));
        var bitmapPath = Path.Combine(workDirectory, "preview.bmp");
        var metadataPath = Path.Combine(workDirectory, "preview.json");

        await s_renderLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(workDirectory);
            using var timeout = new CancellationTokenSource(s_renderTimeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using var process = Process.Start(CreateWorkerStartInfo(
                file.FullName,
                bitmapPath,
                metadataPath,
                maximumDimension,
                dpi));
            if (process == null)
                throw new PdfRenderException("The PDF preview worker could not be started.");

            try
            {
                await process.WaitForExitAsync(linkedCancellation.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                throw new PdfRenderException("PDF rendering timed out.");
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            if (process.ExitCode != 0 || !File.Exists(bitmapPath) || !File.Exists(metadataPath))
                throw new PdfRenderException("The PDF could not be rendered safely.");

            var result = JsonSerializer.Deserialize<PdfRenderResult>(await File.ReadAllTextAsync(metadataPath, cancellationToken));
            if (result == null)
                throw new PdfRenderException("The PDF renderer returned an invalid result.");
            if (result.PageCount == 0)
                return new RenderedPdfPage(result, null);

            await using var bitmapStream = File.OpenRead(bitmapPath);
            return new RenderedPdfPage(result, new Bitmap(bitmapStream));
        }
        finally
        {
            s_renderLock.Release();
            try
            {
                Directory.Delete(workDirectory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static ProcessStartInfo CreateWorkerStartInfo(
        string pdfPath,
        string bitmapPath,
        string metadataPath,
        int maximumDimension,
        int dpi)
    {
        var assemblyPath = typeof(PdfPreviewProvider).Assembly.Location;
        var executablePath = Environment.ProcessPath;
        ProcessStartInfo startInfo;
        if (executablePath != null &&
            Path.GetFileNameWithoutExtension(executablePath).Equals(
                Path.GetFileNameWithoutExtension(assemblyPath), StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo(executablePath);
        }
        else
        {
            startInfo = new ProcessStartInfo("dotnet");
            startInfo.ArgumentList.Add(assemblyPath);
        }

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.ArgumentList.Add(PdfPreviewWorker.Command);
        startInfo.ArgumentList.Add(pdfPath);
        startInfo.ArgumentList.Add(bitmapPath);
        startInfo.ArgumentList.Add(metadataPath);
        startInfo.ArgumentList.Add(maximumDimension.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(dpi.ToString(CultureInfo.InvariantCulture));
        return startInfo;
    }

    private static EmptyPreviewContent Unavailable(BrowserItem item, string reason) =>
        new(item.Name, item.FullPath, $"{item.Size?.ToSize() ?? "Unknown size"} · {reason}");

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
        }
    }

    private sealed class PdfRenderException(string message) : Exception(message);
}

internal sealed record RenderedPdfPage(PdfRenderResult Result, Bitmap Bitmap);
