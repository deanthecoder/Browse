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
public sealed class SettingsServiceTests
{
    [Test]
    public void CheckSettingsRoundTrip()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "settings.json"));
        var service = new SettingsService(file);
        service.Save(new BrowserSettings
        {
            DefaultPath = @"\\server\share",
            ShowDotFolders = true,
            ShowHiddenItems = true
        });

        var loaded = service.Load();

        Assert.That(loaded.DefaultPath, Is.EqualTo(@"\\server\share"));
        Assert.That(loaded.ShowDotFolders, Is.True);
        Assert.That(loaded.ShowHiddenItems, Is.True);
    }
}
