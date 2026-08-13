// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.IO.Compression;
using Browse.Services;
using DTC.Core;

namespace Browse.Tests;

[TestFixture]
public sealed class ArchiveContentServiceTests
{
    [Test]
    public async Task CheckArchiveRootShowsDirectFilesAndFolders()
    {
        using var temp = new TempDirectory();
        var archive = CreateArchive(temp, ("root.txt", "root"), ("docs/readme.txt", "readme"), ("docs/nested/file.txt", "nested"));
        using var service = new ArchiveContentService();

        var items = await service.GetItemsAsync(archive, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(item => item.Name), Is.EqualTo(new[] { "docs", "root.txt" }));
            Assert.That(items.All(item => item.IsArchiveEntry), Is.True);
            Assert.That(items[0].IsDirectory, Is.True);
            Assert.That(items[1].ArchiveCompressedSize, Is.Not.Null);
        });
    }

    [Test]
    public async Task CheckSelectedArchiveFolderExtractsWithItsTopLevelName()
    {
        using var temp = new TempDirectory();
        var archive = CreateArchive(temp, ("docs/readme.txt", "readme"), ("docs/nested/file.txt", "nested"));
        using var service = new ArchiveContentService();
        var folder = (await service.GetItemsAsync(archive, string.Empty)).Single();
        var destination = ((DirectoryInfo)temp).CreateSubdirectory("output");

        await service.ExtractAsync([folder], destination);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(destination.FullName, "docs", "readme.txt")), Is.EqualTo("readme"));
            Assert.That(File.ReadAllText(Path.Combine(destination.FullName, "docs", "nested", "file.txt")), Is.EqualTo("nested"));
        });
    }

    private static FileInfo CreateArchive(DirectoryInfo root, params (string Path, string Contents)[] files)
    {
        var archive = new FileInfo(Path.Combine(root.FullName, "sample.zip"));
        using var zip = ZipFile.Open(archive.FullName, ZipArchiveMode.Create);
        foreach (var (path, contents) in files)
        {
            var entry = zip.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(contents);
        }
        return archive;
    }
}
