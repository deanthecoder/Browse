// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.Specialized;
using Browse.Models;
using Browse.Services;
using Browse.Services.Previews;
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
    [TestCase("/Users/dean/Library/Mobile\\ Documents/com\\~apple\\~CloudDocs/Backup/From\\ matrix", "/Users/dean/Library/Mobile Documents/com~apple~CloudDocs/Backup/From matrix")]
    [TestCase("~/My\\ Folder", "~/My Folder")]
    [TestCase("/tmp/trailing\\", "/tmp/trailing\\")]
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
    public void CheckWindowTitleIncludesActiveFullPath()
    {
        using var temp = new TempDirectory();
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService(),
            new FileOperationService(),
            new SettingsService());
        var column = new FolderColumnViewModel(temp);
        viewModel.Columns.Add(column);

        viewModel.ActivateColumn(column);

        Assert.That(viewModel.WindowTitle, Is.EqualTo($"Browse — {temp.FullName}"));
    }

    [Test]
    public async Task CheckInvalidGoToLeavesBrowserStateUnchanged()
    {
        using var temp = new TempDirectory();
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService(),
            new FileOperationService(),
            new SettingsService());
        var column = new FolderColumnViewModel(temp);
        viewModel.Columns.Add(column);
        viewModel.ShowGoTo();
        viewModel.GoToPath = Path.Combine(temp.FullName, "missing");
        var status = viewModel.StatusText;

        var result = await viewModel.SubmitGoToAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(viewModel.IsGoToVisible, Is.True);
            Assert.That(viewModel.GoToError, Is.EqualTo("Path not found or unavailable."));
            Assert.That(viewModel.StatusText, Is.EqualTo(status));
            Assert.That(viewModel.Columns, Is.EqualTo(new[] { column }));
        });
    }

    [Test]
    public void CheckColumnRefreshReplacesSelectedItemWhenAliasChanges()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "Archive.workzip"));
        File.WriteAllText(file.FullName, "content");
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([new BrowserItem(file)]);
        var selected = column.Items[0];
        column.SetSelection([selected]);

        column.ReplaceItems([new BrowserItem(file, new FileTypeAliasMap([".workzip=.zip"]))]);

        Assert.Multiple(() =>
        {
            Assert.That(column.Items[0], Is.Not.SameAs(selected));
            Assert.That(column.Items[0].IsZipArchive, Is.True);
            Assert.That(column.IsSelectedPath(column.Items[0].FullPath), Is.True);
        });
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

    [Test]
    public void CheckRemovingItemOnlyMutatesOneColumnRow()
    {
        using var temp = new TempDirectory();
        var files = Enumerable.Range(0, 100)
            .Select(index => new FileInfo(Path.Combine(temp.FullName, $"file-{index:D3}.txt")))
            .ToArray();
        foreach (var file in files)
            File.WriteAllText(file.FullName, file.Name);
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems(files.Select(file => new BrowserItem(file)).ToArray());
        var expectedSelection = column.Items[51];
        column.SetSelectionPaths([expectedSelection.FullPath]);
        var changes = new List<NotifyCollectionChangedAction>();
        column.Items.CollectionChanged += (_, e) => changes.Add(e.Action);

        File.Delete(files[50].FullName);
        column.ReplaceItems(temp
            .EnumerateFiles()
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new BrowserItem(file))
            .ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.EqualTo(new[] { NotifyCollectionChangedAction.Remove }));
            Assert.That(column.Items[50], Is.SameAs(expectedSelection));
            Assert.That(column.IsSelectedPath(column.Items[50].FullPath), Is.True);
        });
    }

    [Test]
    public void CheckAddingItemOnlyMutatesOneColumnRow()
    {
        using var temp = new TempDirectory();
        var files = Enumerable.Range(0, 100)
            .Select(index => new FileInfo(Path.Combine(temp.FullName, $"file-{index:D3}.txt")))
            .ToArray();
        foreach (var file in files)
            File.WriteAllText(file.FullName, file.Name);
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems(files.Select(file => new BrowserItem(file)).ToArray());
        var selected = column.Items[75];
        column.SetSelection([selected]);
        var changes = new List<NotifyCollectionChangedAction>();
        column.Items.CollectionChanged += (_, e) => changes.Add(e.Action);

        File.WriteAllText(Path.Combine(temp.FullName, "file-050a.txt"), "inserted");
        column.ReplaceItems(temp
            .EnumerateFiles()
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new BrowserItem(file))
            .ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.EqualTo(new[] { NotifyCollectionChangedAction.Add }));
            Assert.That(column.Items[76], Is.SameAs(selected));
            Assert.That(column.IsSelectedPath(column.Items[76].FullPath), Is.True);
        });
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

    [Test]
    public async Task CheckRapidSelectionDebouncesIntermediatePreviews()
    {
        using var temp = new TempDirectory();
        var first = new FileInfo(Path.Combine(temp.FullName, "first.txt"));
        var second = new FileInfo(Path.Combine(temp.FullName, "second.txt"));
        var third = new FileInfo(Path.Combine(temp.FullName, "third.txt"));
        await File.WriteAllTextAsync(first.FullName, "first");
        await File.WriteAllTextAsync(second.FullName, "second");
        await File.WriteAllTextAsync(third.FullName, "third");
        var provider = new RecordingPreviewProvider();
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService([provider]),
            new FileOperationService(),
            new SettingsService());
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([new BrowserItem(first), new BrowserItem(second), new BrowserItem(third)]);
        viewModel.Columns.Add(column);

        column.SetSelection([column.Items[0]]);
        viewModel.ActivateColumn(column);
        await Task.Delay(50);
        column.SetSelection([column.Items[1]]);
        viewModel.ActivateColumn(column);
        await Task.Delay(50);
        column.SetSelection([column.Items[2]]);
        viewModel.ActivateColumn(column);
        await provider.SecondPreviewCreated.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(provider.CreatedNames, Is.EqualTo(new[] { first.Name, third.Name }));
    }

    [Test]
    public async Task CheckLeisurelySelectionCreatesPreviewImmediately()
    {
        using var temp = new TempDirectory();
        var first = new FileInfo(Path.Combine(temp.FullName, "first.txt"));
        var second = new FileInfo(Path.Combine(temp.FullName, "second.txt"));
        await File.WriteAllTextAsync(first.FullName, "first");
        await File.WriteAllTextAsync(second.FullName, "second");
        var provider = new RecordingPreviewProvider();
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService([provider]),
            new FileOperationService(),
            new SettingsService());
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([new BrowserItem(first), new BrowserItem(second)]);
        viewModel.Columns.Add(column);

        column.SetSelection([column.Items[0]]);
        viewModel.ActivateColumn(column);
        await Task.Delay(550);
        column.SetSelection([column.Items[1]]);
        viewModel.ActivateColumn(column);

        Assert.That(provider.CreatedNames, Is.EqualTo(new[] { first.Name, second.Name }));
    }

    private sealed class RecordingPreviewProvider : IPreviewProvider
    {
        public List<string> CreatedNames { get; } = [];
        public TaskCompletionSource SecondPreviewCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);

        public Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
        {
            CreatedNames.Add(item.Name);
            if (CreatedNames.Count == 2)
                SecondPreviewCreated.TrySetResult();
            return Task.FromResult<PreviewContent>(new EmptyPreviewContent(item.Name));
        }
    }

}
