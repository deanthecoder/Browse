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
using Browse.ViewModels;
using DTC.Core;

namespace Browse.Tests;

[TestFixture]
public sealed class MainWindowViewModelTests
{
    [Test]
    public void CheckMultiplePathsAreSpaceSeparatedAndQuoted()
    {
        using var temp = new TempDirectory();
        var first = new FileInfo(Path.Combine(temp.FullName, "one.txt"));
        var second = new FileInfo(Path.Combine(temp.FullName, "two words.txt"));
        File.WriteAllText(first.FullName, "1");
        File.WriteAllText(second.FullName, "2");

        var result = MainWindowViewModel.JoinPaths([new BrowserItem(first), new BrowserItem(second)]);

        Assert.That(result, Is.EqualTo($"{first.FullName} \"{second.FullName}\""));
    }

    [Test]
    public void CheckNamesCanBeCopiedWithoutParentPaths()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "some file.txt"));
        File.WriteAllText(file.FullName, "test");

        var result = MainWindowViewModel.JoinPaths([new BrowserItem(file)], true);

        Assert.That(result, Is.EqualTo("\"some file.txt\""));
    }

    [TestCase("  \"C:\\Program Files\\Browse\"  ", "C:\\Program Files\\Browse")]
    [TestCase("'/Users/dean/My Folder'", "/Users/dean/My Folder")]
    [TestCase("\"unmatched", "\"unmatched")]
    public void CheckPathInputRemovesMatchingQuotes(string input, string expected)
    {
        var result = MainWindowViewModel.NormalizePathInput(input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void CheckColumnRefreshPreservesSelectedItemInstance()
    {
        using var temp = new TempDirectory();
        var first = new FileInfo(Path.Combine(temp.FullName, "first.txt"));
        var second = new FileInfo(Path.Combine(temp.FullName, "second.txt"));
        var inserted = new FileInfo(Path.Combine(temp.FullName, "before.txt"));
        File.WriteAllText(first.FullName, "first");
        File.WriteAllText(second.FullName, "second");
        File.WriteAllText(inserted.FullName, "inserted");
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([new BrowserItem(first), new BrowserItem(second)]);
        var selected = column.Items[1];
        column.SetSelection([selected]);

        column.ReplaceItems([new BrowserItem(inserted), new BrowserItem(first), new BrowserItem(second)]);

        Assert.That(column.Items[2], Is.SameAs(selected));
    }

    [Test]
    public void CheckColumnRefreshCanSelectReplacementPath()
    {
        using var temp = new TempDirectory();
        var original = new FileInfo(Path.Combine(temp.FullName, "before.txt"));
        var renamed = new FileInfo(Path.Combine(temp.FullName, "after.txt"));
        File.WriteAllText(original.FullName, "before");
        File.WriteAllText(renamed.FullName, "after");
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([new BrowserItem(original)]);

        column.SetSelectionPaths([renamed.FullName]);
        column.ReplaceItems([new BrowserItem(renamed)]);

        Assert.That(column.IsSelectedPath(column.Items[0].FullPath), Is.True);
    }

    [TestCase(1, "third.txt")]
    [TestCase(2, "second.txt")]
    public void CheckColumnChoosesAdjacentItemAfterRemoval(int removedIndex, string expectedName)
    {
        using var temp = new TempDirectory();
        var files = new[] { "first.txt", "second.txt", "third.txt" }
            .Select(name => new FileInfo(Path.Combine(temp.FullName, name)))
            .ToArray();
        foreach (var file in files)
            File.WriteAllText(file.FullName, file.Name);
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems(files.Select(file => new BrowserItem(file)).ToArray());

        var selectedPath = column.GetSelectionPathAfterRemoving([column.Items[removedIndex]]);

        Assert.That(selectedPath, Is.EqualTo(Path.Combine(temp.FullName, expectedName)));
    }

}
