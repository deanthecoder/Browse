// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Input;
using Browse.Models;
using Browse.Views;

namespace Browse.Tests;

/// <summary>
/// Verifies interaction decisions made by the main Browse window.
/// </summary>
/// <remarks>
/// Pure drag-result rules are tested without requiring a desktop drag session.
/// </remarks>
[TestFixture]
public sealed class MainWindowTests
{
    [TestCase("tool.exe", ".exe", true)]
    [TestCase("tool.exe", "too", true)]
    [TestCase("tool.exe", ".dll", false)]
    [TestCase("folder.exe", ".exe", false, true)]
    public void CheckTypedNavigationIncludesFileExtension(
        string name,
        string prefix,
        bool expected,
        bool isDirectory = false)
    {
        FileSystemInfo info = isDirectory ? new DirectoryInfo(name) : new FileInfo(name);

        var result = MainWindow.MatchesTypedPrefix(new BrowserItem(info), prefix);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(DragDropEffects.None, true, false, true)]
    [TestCase(DragDropEffects.None, false, false, false)]
    [TestCase(DragDropEffects.None, true, true, false)]
    [TestCase(DragDropEffects.Move, true, false, false)]
    public void CheckFavoriteRemovalFollowsDragOutcome(
        DragDropEffects result,
        bool outside,
        bool canceled,
        bool expected)
    {
        Assert.That(MainWindow.ShouldRemoveFavoriteAfterDrag(result, outside, canceled), Is.EqualTo(expected));
    }
}
