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
    public async Task CheckPendingPdfPreviewShowsMetadataWhileRendering()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "large.pdf"));
        await File.WriteAllTextAsync(file.FullName, "large pdf contents");

        var result = MainWindowViewModel.CreatePendingPreview(new BrowserItem(file));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<LoadingPreviewContent>());
            Assert.That(result.Details, Does.Contain($"{file.Length:N0} bytes"));
            Assert.That(((LoadingPreviewContent)result).Message, Is.EqualTo("Rendering PDF preview…"));
        });
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
    public void CheckColumnFilterMatchesNamesAndSurvivesRefresh()
    {
        using var temp = new TempDirectory();
        var column = new FolderColumnViewModel(temp);
        BrowserItem Item(string name) => new(new FileInfo(Path.Combine(temp.FullName, name)));
        column.ReplaceItems([Item("alpha.png"), Item("beta.txt")]);
        column.FilterText = ".PNG";
        Assert.That(column.Items.Select(item => item.Name), Is.EqualTo(new[] { "alpha.png" }));

        column.ReplaceItems([Item("alpha.png"), Item("beta.txt"), Item("new.png")]);
        Assert.That(column.Items.Select(item => item.Name), Is.EqualTo(new[] { "alpha.png", "new.png" }));
        column.FilterText = "missing";
        Assert.That(column.Items, Is.Empty);
        column.CloseFilter();
        Assert.That(column.Items, Has.Count.EqualTo(3));
        Assert.That(column.IsFilterVisible, Is.False);
    }

    [Test]
    public void CheckFilteringRetainsDateHeadingAndClearsHiddenSelection()
    {
        using var temp = new TempDirectory();
        var first = new BrowserItem(new FileInfo(Path.Combine(temp.FullName, "alpha.txt")), groupHeading: "Today");
        var second = new BrowserItem(new FileInfo(Path.Combine(temp.FullName, "beta.txt")));
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([first, second]);
        column.SetSelection([first]);

        column.FilterText = "beta";

        Assert.That(column.Items.Single().GroupHeading, Is.EqualTo("Today"));
        Assert.That(column.IsSelectedPath(first.FullPath), Is.False);
        column.CloseFilter();
        Assert.That(column.Items.Select(item => item.GroupHeading), Is.EqualTo(new[] { "Today", null }));
    }

    [Test]
    public void CheckDriveTooltipIncludesCapacityAndUsage()
    {
        var tip = SidebarEntryViewModel.FormatDriveUsage("C:\\", 1000, 250);
        Assert.That(tip, Does.StartWith("C:\\\n"));
        Assert.That(tip, Does.Contain("750 bytes used of"));
        Assert.That(tip, Does.Contain("75"));
        Assert.That(tip, Does.EndWith("250 bytes free"));
    }

    [TestCase(0, 0)]
    [TestCase(100, -1)]
    [TestCase(100, 101)]
    public void CheckUnavailableDriveUsageHasReadableFallback(long total, long free)
    {
        Assert.That(SidebarEntryViewModel.FormatDriveUsage("drive", total, free),
            Is.EqualTo("drive\nDisk usage unavailable"));
    }

    [Test]
    public void CheckInvalidDriveDoesNotThrowWhenReadingTooltip()
    {
        var entry = new SidebarEntryViewModel("Unavailable", "invalid\0drive", true);
        Assert.That(entry.GetDriveToolTip(), Does.EndWith("Disk usage unavailable"));
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
    public async Task CheckGoToFileSelectsFileInContainingFolder()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "target.txt"));
        await File.WriteAllTextAsync(file.FullName, "selected");
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService(),
            new FileOperationService(),
            new SettingsService());
        viewModel.ShowGoTo();
        viewModel.GoToPath = file.FullName;

        var result = await viewModel.SubmitGoToAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(viewModel.CurrentPath, Is.EqualTo(temp.FullName));
            Assert.That(viewModel.SelectedItems.Select(item => item.FullPath), Is.EqualTo(new[] { file.FullName }));
            Assert.That(viewModel.Columns.Single().IsSelectedPath(file.FullName), Is.True);
        });
    }

    [TestCase("home", Environment.SpecialFolder.UserProfile)]
    [TestCase("  \"HoMe\"  ", Environment.SpecialFolder.UserProfile)]
    [TestCase("desktop", Environment.SpecialFolder.DesktopDirectory)]
    [TestCase("appdata", Environment.SpecialFolder.ApplicationData)]
    [TestCase("LOCALAPPDATA", Environment.SpecialFolder.LocalApplicationData)]
    [TestCase("programdata", Environment.SpecialFolder.CommonApplicationData)]
    public async Task CheckGoToSpecialLocation(string input, Environment.SpecialFolder folder)
    {
        if (!OperatingSystem.IsWindows() && input.Trim().ToLowerInvariant() is "appdata" or "localappdata" or "programdata")
            Assert.Ignore("Windows-specific alias.");
        var expected = Environment.GetFolderPath(folder);
        if (!Directory.Exists(expected))
            Assert.Ignore("Special folder is unavailable on this machine.");
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(), new PreviewService(), new FileOperationService(), new SettingsService());
        viewModel.ShowGoTo();
        viewModel.GoToPath = input;

        Assert.That(await viewModel.SubmitGoToAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentPath, Is.EqualTo(expected));
            Assert.That(viewModel.IsGoToVisible, Is.False);
        });
    }

    [Test]
    public async Task CheckMissingSpecialLocationSubpathLeavesDialogOpen()
    {
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(), new PreviewService(), new FileOperationService(), new SettingsService());
        viewModel.ShowGoTo();
        viewModel.GoToPath = $"home/{Guid.NewGuid():N}";

        Assert.That(await viewModel.SubmitGoToAsync(), Is.False);
        Assert.That(viewModel.IsGoToVisible, Is.True);
        Assert.That(viewModel.GoToError, Is.EqualTo("Path not found or unavailable."));
    }

    [Test]
    public async Task CheckInitializeWithFileSelectsFileInContainingFolder()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "clipboard.txt"));
        await File.WriteAllTextAsync(file.FullName, "selected");
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService(),
            new FileOperationService(),
            new SettingsService());

        await viewModel.InitializeAsync(file.FullName);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentPath, Is.EqualTo(temp.FullName));
            Assert.That(viewModel.SelectedItems.Select(item => item.FullPath), Is.EqualTo(new[] { file.FullName }));
            Assert.That(viewModel.Columns.Single().IsSelectedPath(file.FullName), Is.True);
        });
    }

    [Test]
    public async Task CheckSlowShellLaunchDoesNotBlockSelectionOrLaunchTwice()
    {
        using var temp = new TempDirectory();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launches = 0;
        var service = new FileOperationService(_ =>
        {
            Interlocked.Increment(ref launches);
            started.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(5));
        });
        using var model = new MainWindowViewModel(new DirectoryContentService(), new PreviewService(), service, new SettingsService());
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([new BrowserItem(new FileInfo(Path.Combine(temp.FullName, "slow.exe")))]);
        model.Columns.Add(column);
        model.SetContextSelection(column, [column.Items[0]]);
        var opening = model.OpenSelectedAsync();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(opening.IsCompleted, Is.False);
            Assert.That(model.StatusText, Does.StartWith("Opening slow.exe"));
            await model.OpenSelectedAsync();
            Assert.That(launches, Is.EqualTo(1));
            model.SetContextSelection(column, []);
        }
        finally
        {
            release.Set();
            await opening.WaitAsync(TimeSpan.FromSeconds(2));
        }
        Assert.That(model.StatusText, Is.EqualTo("Opened slow.exe."));
    }

    [TestCase(1223, "Open canceled.")]
    [TestCase(2, "Could not open missing.exe:")]
    public async Task CheckShellLaunchErrorsAreReported(int errorCode, string expected)
    {
        using var temp = new TempDirectory();
        var service = new FileOperationService(_ => throw new System.ComponentModel.Win32Exception(errorCode));
        using var model = new MainWindowViewModel(new DirectoryContentService(), new PreviewService(), service, new SettingsService());
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([new BrowserItem(new FileInfo(Path.Combine(temp.FullName, "missing.exe")))]);
        model.Columns.Add(column);
        model.SetContextSelection(column, [column.Items[0]]);

        await model.OpenSelectedFileAsync();

        Assert.That(model.StatusText, Does.StartWith(expected));
    }

    [Test]
    public async Task CheckOpenSelectedFileIgnoresFolder()
    {
        using var temp = new TempDirectory();
        var folder = ((DirectoryInfo)temp).CreateSubdirectory("folder");
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService(),
            new FileOperationService(),
            new SettingsService());
        await viewModel.NavigateToAsync(temp.FullName);
        var column = viewModel.Columns.Single();
        var item = column.Items.Single(candidate => candidate.FullPath == folder.FullName);
        await viewModel.SelectAsync(column, [item]);
        var columns = viewModel.Columns.ToArray();

        await viewModel.OpenSelectedFileAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentPath, Is.EqualTo(folder.FullName));
            Assert.That(viewModel.Columns, Is.EqualTo(columns));
            Assert.That(viewModel.SelectedItems, Is.EqualTo(new[] { item }));
        });
    }

    [Test]
    public async Task CheckCopyAndPasteSelectedFolderDuplicatesBesideOriginal()
    {
        using var temp = new TempDirectory();
        var folder = ((DirectoryInfo)temp).CreateSubdirectory("folder");
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService(),
            new FileOperationService(),
            new SettingsService());
        await viewModel.NavigateToAsync(temp.FullName);
        var column = viewModel.Columns.Single();
        var item = column.Items.Single(candidate => candidate.FullPath == folder.FullName);
        await viewModel.SelectAsync(column, [item]);

        viewModel.CopySelection(false);
        await viewModel.PasteAsync();

        Assert.That(Directory.Exists(Path.Combine(temp.FullName, "folder (2)")), Is.True);
    }

    [Test]
    public async Task CheckDuplicateSelectionCopiesItemsBesideOriginals()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "notes.txt"));
        await File.WriteAllTextAsync(file.FullName, "notes");
        using var viewModel = new MainWindowViewModel(
            new DirectoryContentService(),
            new PreviewService(),
            new FileOperationService(),
            new SettingsService());
        await viewModel.NavigateToAsync(temp.FullName);
        var column = viewModel.Columns.Single();
        var item = column.Items.Single(candidate => candidate.FullPath == file.FullName);
        await viewModel.SelectAsync(column, [item]);

        await viewModel.DuplicateSelectionAsync();

        Assert.That(File.ReadAllText(Path.Combine(temp.FullName, "notes (2).txt")), Is.EqualTo("notes"));
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

    [Test]
    public async Task CheckDeletionWaitsForCanceledPreviewToReleaseTheFile()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "locked.exe"));
        File.WriteAllText(file.FullName, "preview");
        var provider = new DeferredPreviewProvider();
        using var model = new MainWindowViewModel(new DirectoryContentService(), new PreviewService([provider]),
            new FileOperationService(), new SettingsService());
        var column = new FolderColumnViewModel(temp);
        column.ReplaceItems([new BrowserItem(file)]);
        model.Columns.Add(column);
        await model.SelectAsync(column, [column.Items[0]]);

        var deletion = model.DeleteSelectionAsync();
        try
        {
            Assert.That(provider.Cancellation.IsCancellationRequested, Is.True);
            Assert.That(deletion.IsCompleted, Is.False, "Cancellation alone must not start deletion.");
            Assert.That(File.Exists(file.FullName), Is.True);
            await model.DeleteSelectionAsync();
            Assert.That(deletion.IsCompleted, Is.False, "Repeated Delete must not start a second operation.");
        }
        finally
        {
            // Simulate external removal so this test never writes to the user's recycle bin.
            File.Delete(file.FullName);
            provider.Finished.TrySetResult();
            await deletion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Simulates native preview work that cannot stop immediately on cancellation.</summary>
    /// <remarks>The test controls when the provider has released its resources.</remarks>
    private sealed class DeferredPreviewProvider : IPreviewProvider
    {
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Cancellation { get; private set; }

        public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);

        public async Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
        {
            Cancellation = cancellationToken;
            await Finished.Task;
            throw new IOException("The canceled preview has finished.");
        }
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
