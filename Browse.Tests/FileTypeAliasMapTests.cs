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
using Material.Icons;

namespace Browse.Tests;

[TestFixture]
public sealed class FileTypeAliasMapTests
{
    [Test]
    public void CheckMappingsAreNormalized()
    {
        var valid = FileTypeAliasMap.TryNormalize(" WORKZIP = ZIP\n.slnx=.xml ", out var mappings, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(mappings, Is.EqualTo(new[] { ".workzip=.zip", ".slnx=.xml" }));
            Assert.That(error, Is.Null);
        });
    }

    [TestCase("invalid")]
    [TestCase(".one=.zip=.two")]
    [TestCase(".one=.zip\n.one=.xml")]
    public void CheckInvalidMappingsAreRejected(string text)
    {
        var valid = FileTypeAliasMap.TryNormalize(text, out var mappings, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(mappings, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        });
    }

    [Test]
    public void CheckAliasesCanBeChained()
    {
        var aliases = new FileTypeAliasMap([".first=.second", ".second=.zip"]);

        Assert.That(aliases.ResolveExtension("Archive.FIRST"), Is.EqualTo(".zip"));
    }

    [Test]
    public void CheckCircularAliasesFallBackToTheRealExtension()
    {
        var aliases = new FileTypeAliasMap([".first=.second", ".second=.first"]);

        Assert.That(aliases.ResolveExtension("Archive.first"), Is.EqualTo(".first"));
    }

    [Test]
    public void CheckZipAliasDrivesArchiveBehaviorAndIcon()
    {
        using var temp = new DTC.Core.TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "Archive.workzip"));
        File.WriteAllText(file.FullName, "content");

        var item = new BrowserItem(file, new FileTypeAliasMap([".workzip=.zip"]));

        Assert.Multiple(() =>
        {
            Assert.That(item.EffectiveExtension, Is.EqualTo(".zip"));
            Assert.That(item.IsZipArchive, Is.True);
            Assert.That(item.Icon, Is.EqualTo(MaterialIconKind.ZipBox));
        });
    }
}
