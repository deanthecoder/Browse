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
using DTC.Core;

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

    [TestCase(20, 100, 12, 1, 31)]
    [TestCase(20, 100, 12, -1, 9)]
    [TestCase(95, 100, 12, 1, 99)]
    [TestCase(3, 100, 12, -1, 0)]
    public void CheckPageNavigationSelectsItemOneViewportAway(
        int currentIndex,
        int itemCount,
        int visibleItemCount,
        int direction,
        int expected)
    {
        Assert.That(
            MainWindow.GetPageTargetIndex(currentIndex, itemCount, visibleItemCount, direction),
            Is.EqualTo(expected));
    }

    [Test]
    public void CheckClipboardFileObjectProvidesNewWindowPath()
    {
        using var temp = new TempDirectory();

        var result = MainWindow.GetClipboardNavigationPath([temp.FullName], null);

        Assert.That(result, Is.EqualTo(temp.FullName));
    }

    [Test]
    public void CheckClipboardTextProvidesNewWindowPath()
    {
        using var temp = new TempDirectory();

        var result = MainWindow.GetClipboardNavigationPath([], $"\"{temp.FullName}\"");

        Assert.That(result, Is.EqualTo(temp.FullName));
    }

    [TestCase(2000, 1000, 1000, 700, 0.5)]
    [TestCase(500, 400, 1000, 700, 1.0)]
    [TestCase(1000, 2000, 800, 500, 0.25)]
    public void CheckImageFitZoomNeverEnlargesPastOriginalSize(
        int imageWidth,
        int imageHeight,
        double viewportWidth,
        double viewportHeight,
        double expected)
    {
        Assert.That(
            ImagePreviewViewer.GetFitZoom(
                new Avalonia.PixelSize(imageWidth, imageHeight),
                new Avalonia.Size(viewportWidth, viewportHeight)),
            Is.EqualTo(expected));
    }

    [TestCase(1.0, 0.1, 1.1)]
    [TestCase(2.0, -0.25, 1.5)]
    [TestCase(32.0, 0.5, 32.0)]
    public void CheckTouchpadMagnificationAdjustsCurrentZoom(
        double zoom,
        double magnification,
        double expected)
    {
        Assert.That(ImagePreviewViewer.ApplyMagnification(zoom, magnification), Is.EqualTo(expected));
    }

    [TestCase(0.5, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality)]
    [TestCase(1.0, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality)]
    [TestCase(1.01, Avalonia.Media.Imaging.BitmapInterpolationMode.None)]
    public void CheckImageZoomUsesCrispUpscalingAndSmoothDownscaling(
        double zoom,
        Avalonia.Media.Imaging.BitmapInterpolationMode expected)
    {
        Assert.That(ImagePreviewViewer.GetInterpolationMode(zoom), Is.EqualTo(expected));
    }
}
