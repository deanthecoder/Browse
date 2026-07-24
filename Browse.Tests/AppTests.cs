// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;

namespace Browse.Tests;

/// <summary>
/// Verifies application-level window behavior.
/// </summary>
/// <remarks>
/// Position calculations are tested independently of a desktop session.
/// </remarks>
[TestFixture]
public sealed class AppTests
{
    [Test]
    public void CheckNewWindowIsOffsetFromSource()
    {
        var position = App.GetOffsetWindowPosition(
            new PixelPoint(100, 200),
            new PixelRect(0, 0, 1920, 1080),
            new PixelSize(1000, 400));

        Assert.That(position, Is.EqualTo(new PixelPoint(132, 232)));
    }

    [Test]
    public void CheckNewWindowRemainsInsideWorkingArea()
    {
        var position = App.GetOffsetWindowPosition(
            new PixelPoint(1000, 700),
            new PixelRect(0, 0, 1200, 800),
            new PixelSize(600, 300));

        Assert.That(position, Is.EqualTo(new PixelPoint(600, 500)));
    }
}
