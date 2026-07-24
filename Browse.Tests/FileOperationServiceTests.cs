// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Security.Cryptography;
using System.IO.Compression;
using Browse.Models;
using Browse.Services;
using DTC.Core;

namespace Browse.Tests;

[TestFixture]
public sealed class FileOperationServiceTests
{
    [Test]
    public async Task CheckFileCanBeHashedAndEncoded()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "hello.txt"));
        await File.WriteAllTextAsync(file.FullName, "hello");
        var service = new FileOperationService();

        var md5 = await service.CalculateHashAsync(file, HashAlgorithmName.MD5);
        var sha256 = await service.CalculateHashAsync(file, HashAlgorithmName.SHA256);
        var base64 = await service.EncodeBase64Async(file);

        Assert.Multiple(() =>
        {
            Assert.That(md5, Is.EqualTo("5d41402abc4b2a76b9719d911017c592"));
            Assert.That(sha256, Is.EqualTo("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824"));
            Assert.That(base64, Is.EqualTo("aGVsbG8="));
        });
    }

    [Test]
    public async Task CheckFolderSizeIncludesNestedFiles()
    {
        using var temp = new TempDirectory();
        var root = (DirectoryInfo)temp;
        var nested = root.CreateSubdirectory("nested");
        File.WriteAllBytes(Path.Combine(root.FullName, "a.bin"), new byte[13]);
        File.WriteAllBytes(Path.Combine(nested.FullName, "b.bin"), new byte[29]);

        var size = await new FileOperationService().CalculateFolderSizeAsync(root);

        Assert.That(size, Is.EqualTo(42));
    }

    [Test]
    public void CheckAvailablePathAddsNumberedSuffix()
    {
        using var temp = new TempDirectory();
        var original = Path.Combine(temp.FullName, "Archive.zip");
        File.WriteAllText(original, "existing");

        var result = FileOperationService.GetAvailablePath(original);

        Assert.That(result, Is.EqualTo(Path.Combine(temp.FullName, "Archive (2).zip")));
    }

    [Test]
    public async Task CheckCopyIntoOwnFolderIsIgnored()
    {
        using var temp = new TempDirectory();
        var root = (DirectoryInfo)temp;
        var folder = root.CreateSubdirectory("folder");

        await new FileOperationService().CopyAsync([new BrowserItem(folder)], root, false);

        Assert.That(Directory.Exists(Path.Combine(temp.FullName, "folder (2)")), Is.False);
        Assert.That(folder.Exists, Is.True);
    }

    [Test]
    public async Task CheckSingleArchivedFileIsExpandedAlongsideZip()
    {
        using var temp = new TempDirectory();
        var zip = new FileInfo(Path.Combine(temp.FullName, "Archive.zip"));
        using (var archive = ZipFile.Open(zip.FullName, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("nested/readme.txt");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("expanded");
        }
        await File.WriteAllTextAsync(Path.Combine(temp.FullName, "readme.txt"), "existing");

        var output = await new FileOperationService().ExpandZipAsync(zip);

        Assert.Multiple(() =>
        {
            Assert.That(output, Is.TypeOf<FileInfo>());
            Assert.That(output.Name, Is.EqualTo("readme (2).txt"));
            Assert.That(File.ReadAllText(output.FullName), Is.EqualTo("expanded"));
        });
    }

    [Test]
    public async Task CheckMultipleArchivedFilesAreExpandedIntoNamedFolder()
    {
        using var temp = new TempDirectory();
        var zip = new FileInfo(Path.Combine(temp.FullName, "Archive.zip"));
        using (var archive = ZipFile.Open(zip.FullName, ZipArchiveMode.Create))
        {
            archive.CreateEntry("one.txt").Open().Dispose();
            archive.CreateEntry("nested/two.txt").Open().Dispose();
        }

        var output = await new FileOperationService().ExpandZipAsync(zip);

        Assert.Multiple(() =>
        {
            Assert.That(output, Is.TypeOf<DirectoryInfo>());
            Assert.That(output.Name, Is.EqualTo("Archive"));
            Assert.That(File.Exists(Path.Combine(output.FullName, "one.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(output.FullName, "nested", "two.txt")), Is.True);
        });
    }

    [Test]
    public void CheckUnsafeArchivePathIsRejected()
    {
        using var temp = new TempDirectory();
        var zip = new FileInfo(Path.Combine(temp.FullName, "Archive.zip"));
        using (var archive = ZipFile.Open(zip.FullName, ZipArchiveMode.Create))
        {
            archive.CreateEntry("../outside.txt").Open().Dispose();
            archive.CreateEntry("safe.txt").Open().Dispose();
        }

        Assert.That(
            async () => await new FileOperationService().ExpandZipAsync(zip),
            Throws.TypeOf<InvalidDataException>());
        Assert.That(File.Exists(Path.Combine(temp.FullName, "outside.txt")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(temp.FullName, "Archive")), Is.False);
    }
}
