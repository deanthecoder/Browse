// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.


using Browse.Services;

namespace Browse.Tests;

[TestFixture]
public sealed class SpecialLocationResolverTests
{
    [TestCase("temp")]
    [TestCase("TEMP")]
    [TestCase("temp/child")]
    public void CheckTempLocation(string input)
    {
        var expected = input.Contains('/') ? Path.Combine(Path.GetTempPath(), "child") : Path.GetTempPath();
        Assert.That(SpecialLocationResolver.Expand(input), Is.EqualTo(expected));
    }

    [TestCase("home/child", "child")]
    [TestCase("HOME/child/grandchild", "child/grandchild")]
    public void CheckAliasSubpaths(string input, string suffix)
    {
        Assert.That(Path.GetFullPath(SpecialLocationResolver.Expand(input)),
            Is.EqualTo(Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), suffix))));
    }

    [TestCase("./home")]
    [TestCase("homework")]
    [TestCase("/tmp/home")]
    [TestCase(@"C:\home\file")]
    [TestCase(@"\\server\home")]
    public void CheckOrdinaryPathsAreUnchanged(string path) =>
        Assert.That(SpecialLocationResolver.Expand(path), Is.EqualTo(path));

    [TestCase("downloads")]
    [TestCase("public")]
    public void CheckPlatformFolders(string alias)
    {
        var path = SpecialLocationResolver.Expand(alias);
        // Windows may report an unavailable known folder with an empty path.
        if (OperatingSystem.IsWindows())
            Assert.That(path == string.Empty || Path.IsPathFullyQualified(path), Is.True);
        else
            Assert.That(path, Is.EqualTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                alias == "downloads" ? "Downloads" : "Public")));
    }

    [Test]
    public void CheckWindowsBackslashSubpath()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Windows path separator.");
        Assert.That(SpecialLocationResolver.Expand(@"localappdata\Temp"),
            Is.EqualTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")));
    }
}

