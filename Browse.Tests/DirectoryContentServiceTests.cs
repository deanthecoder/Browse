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
public sealed class DirectoryContentServiceTests
{
    [Test]
    public async Task CheckFilesAndFoldersAreSortedTogether()
    {
        using var temp = new TempDirectory();
        var directory = (DirectoryInfo)temp;
        directory.CreateSubdirectory("bravo");
        File.WriteAllText(Path.Combine(temp.FullName, "alpha.txt"), "a");
        File.WriteAllText(Path.Combine(temp.FullName, "charlie.txt"), "c");

        var items = await new DirectoryContentService().GetItemsAsync(directory, new BrowserSettings
        {
            ShowDotFolders = true,
            ShowHiddenItems = true
        });

        Assert.That(items.Select(item => item.Name), Is.EqualTo(new[] { "alpha.txt", "bravo", "charlie.txt" }));
    }

    [Test]
    public async Task GivenDotFolderHiddenCheckItIsFiltered()
    {
        using var temp = new TempDirectory();
        var directory = (DirectoryInfo)temp;
        directory.CreateSubdirectory(".git");
        directory.CreateSubdirectory("source");

        var items = await new DirectoryContentService().GetItemsAsync(directory, new BrowserSettings
        {
            ShowDotFolders = false,
            ShowHiddenItems = true
        });

        Assert.That(items.Select(item => item.Name), Is.EqualTo(new[] { "source" }));
    }

    [Test]
    public async Task GivenDotFileHiddenCheckItIsFilteredWithOtherHiddenItems()
    {
        using var temp = new TempDirectory();
        var directory = (DirectoryInfo)temp;
        File.WriteAllText(Path.Combine(temp.FullName, ".pub"), "key");
        File.WriteAllText(Path.Combine(temp.FullName, "visible.txt"), "text");

        var items = await new DirectoryContentService().GetItemsAsync(directory, new BrowserSettings
        {
            ShowDotFolders = true,
            ShowHiddenItems = false
        });

        Assert.That(items.Select(item => item.Name), Is.EqualTo(new[] { "visible.txt" }));
    }
}
