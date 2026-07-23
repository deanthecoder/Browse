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
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Browse.Models;
using Microsoft.VisualBasic.FileIO;

namespace Browse.Services;

/// <summary>
/// Performs file operations without blocking the UI thread.
/// </summary>
/// <remarks>
/// Operations accept cancellation where the underlying platform APIs permit it.
/// </remarks>
public sealed class FileOperationService
{
    private const long MaxBase64Bytes = 32 * 1024 * 1024;
    public Task CopyAsync(IReadOnlyList<BrowserItem> items, DirectoryInfo destination, bool move, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            destination.Create();
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var requestedPath = Path.Combine(destination.FullName, item.Name);
                if (PathsEqual(item.FullPath, requestedPath))
                    continue;
                var targetPath = GetAvailablePath(requestedPath);
                if (item.IsDirectory)
                {
                    if (move)
                        MoveDirectory(new DirectoryInfo(item.FullPath), new DirectoryInfo(targetPath), cancellationToken);
                    else
                        CopyDirectory(new DirectoryInfo(item.FullPath), new DirectoryInfo(targetPath), cancellationToken);
                }
                else if (move)
                {
                    MoveFile(new FileInfo(item.FullPath), new FileInfo(targetPath));
                }
                else
                {
                    File.Copy(item.FullPath, targetPath);
                }
            }
        }, cancellationToken);

    public Task MoveToTrashAsync(IReadOnlyList<BrowserItem> items, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (OperatingSystem.IsWindows())
                {
                    if (item.IsDirectory)
                        FileSystem.DeleteDirectory(item.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    else
                        FileSystem.DeleteFile(item.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    var trash = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash"));
                    trash.Create();
                    var destination = GetAvailablePath(Path.Combine(trash.FullName, item.Name));
                    if (item.IsDirectory)
                        Directory.Move(item.FullPath, destination);
                    else
                        File.Move(item.FullPath, destination);
                }
                else
                {
                    var startInfo = new ProcessStartInfo("gio") { UseShellExecute = false };
                    startInfo.ArgumentList.Add("trash");
                    startInfo.ArgumentList.Add(item.FullPath);
                    using var process = Process.Start(startInfo) ?? throw new IOException("Could not start the recycle-bin operation.");
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new IOException($"Could not move {item.Name} to the recycle bin.");
                }
            }
        }, cancellationToken);

    public Task RenameAsync(BrowserItem item, string newName) => Task.Run(() =>
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("The new name is not valid.", nameof(newName));
        var destination = Path.Combine(Path.GetDirectoryName(item.FullPath) ?? string.Empty, newName.Trim());
        if (File.Exists(destination) || Directory.Exists(destination))
            throw new IOException("An item with that name already exists.");
        if (item.IsDirectory)
            Directory.Move(item.FullPath, destination);
        else
            File.Move(item.FullPath, destination);
    });

    public Task<long> CalculateFolderSizeAsync(DirectoryInfo directory, CancellationToken cancellationToken = default) =>
        Task.Run(() => CalculateFolderSize(directory, cancellationToken), cancellationToken);

    public Task CreateZipAsync(IReadOnlyList<BrowserItem> items, FileInfo output, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var archive = ZipFile.Open(output.FullName, ZipArchiveMode.Create);
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.IsDirectory)
                    AddDirectoryToArchive(archive, new DirectoryInfo(item.FullPath), item.Name, cancellationToken);
                else
                    archive.CreateEntryFromFile(item.FullPath, item.Name, CompressionLevel.Optimal);
            }
        }, cancellationToken);

    public void Open(BrowserItem item)
    {
        var startInfo = new ProcessStartInfo(item.FullPath) { UseShellExecute = true };
        Process.Start(startInfo);
    }

    public async Task<string> CalculateHashAsync(
        FileInfo file,
        HashAlgorithmName algorithm,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using HashAlgorithm hashAlgorithm = algorithm == HashAlgorithmName.MD5
            ? MD5.Create()
            : algorithm == HashAlgorithmName.SHA256
                ? SHA256.Create()
                : throw new ArgumentOutOfRangeException(nameof(algorithm));
        var hash = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<string> EncodeBase64Async(FileInfo file, CancellationToken cancellationToken = default)
    {
        file.Refresh();
        if (file.Length > MaxBase64Bytes)
            throw new IOException("Base64 copying is limited to files of 32 MB or less.");
        var bytes = await File.ReadAllBytesAsync(file.FullName, cancellationToken);
        return Convert.ToBase64String(bytes);
    }

    public void OpenTerminal(DirectoryInfo directory, string configuredCommand = null)
    {
        if (OperatingSystem.IsWindows() && configuredCommand is "PowerShell" or "Command Prompt")
        {
            var executable = configuredCommand == "PowerShell" ? "powershell.exe" : "cmd.exe";
            Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = directory.FullName, UseShellExecute = true });
            return;
        }
        if (!string.IsNullOrWhiteSpace(configuredCommand) && configuredCommand != "Windows Terminal")
        {
            Process.Start(new ProcessStartInfo(configuredCommand) { WorkingDirectory = directory.FullName, UseShellExecute = true });
            return;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var startInfo = new ProcessStartInfo("wt.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add(directory.FullName);
            try
            {
                Process.Start(startInfo);
            }
            catch (Exception)
            {
                Process.Start(new ProcessStartInfo("cmd.exe") { WorkingDirectory = directory.FullName, UseShellExecute = true });
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var startInfo = new ProcessStartInfo("open") { UseShellExecute = false };
            startInfo.ArgumentList.Add("-a");
            startInfo.ArgumentList.Add("Terminal");
            startInfo.ArgumentList.Add(directory.FullName);
            Process.Start(startInfo);
        }
        else
        {
            Process.Start(new ProcessStartInfo("xdg-terminal-exec") { WorkingDirectory = directory.FullName, UseShellExecute = true });
        }
    }

    private static long CalculateFolderSize(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        long total = 0;
        IEnumerable<FileSystemInfo> children;
        try
        {
            children = directory.EnumerateFileSystemInfos();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (child is FileInfo file)
                    total += file.Length;
                else if (!child.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    total += CalculateFolderSize((DirectoryInfo)child, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Inaccessible items do not prevent a useful partial total.
            }
        }
        return total;
    }

    private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination, CancellationToken cancellationToken)
    {
        destination.Create();
        foreach (var file in source.EnumerateFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            file.CopyTo(Path.Combine(destination.FullName, file.Name));
        }
        foreach (var child in source.EnumerateDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!child.Attributes.HasFlag(FileAttributes.ReparsePoint))
                CopyDirectory(child, destination.CreateSubdirectory(child.Name), cancellationToken);
        }
    }

    private static void MoveDirectory(DirectoryInfo source, DirectoryInfo destination, CancellationToken cancellationToken)
    {
        try
        {
            source.MoveTo(destination.FullName);
        }
        catch (IOException)
        {
            CopyDirectory(source, destination, cancellationToken);
            source.Delete(true);
        }
    }

    private static void MoveFile(FileInfo source, FileInfo destination)
    {
        try
        {
            source.MoveTo(destination.FullName);
        }
        catch (IOException)
        {
            source.CopyTo(destination.FullName);
            source.Delete();
        }
    }

    private static void AddDirectoryToArchive(ZipArchive archive, DirectoryInfo directory, string prefix, CancellationToken cancellationToken)
    {
        foreach (var file in directory.EnumerateFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            archive.CreateEntryFromFile(file.FullName, $"{prefix}/{file.Name}", CompressionLevel.Optimal);
        }
        foreach (var child in directory.EnumerateDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!child.Attributes.HasFlag(FileAttributes.ReparsePoint))
                AddDirectoryToArchive(archive, child, $"{prefix}/{child.Name}", cancellationToken);
        }
    }

    public static string GetAvailablePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return path;
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var extension = Path.GetExtension(path);
        var name = Path.GetFileNameWithoutExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            comparison);
    }
}
