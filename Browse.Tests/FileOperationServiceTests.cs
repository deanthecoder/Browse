// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Browse.Models;
using Browse.Services;
using DTC.Core;

namespace Browse.Tests;

[TestFixture]
public sealed class FileOperationServiceTests
{
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
}
