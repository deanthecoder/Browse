// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.IO.Compression;
using Browse.Converters;
using Browse.Models;
using Browse.Services;
using DTC.Core;

namespace Browse.Tests;

[TestFixture]
public sealed class PreviewServiceTests
{
    [Test]
    public async Task CheckZipPreviewListsArchiveContents()
    {
        using var temp = new TempDirectory();
        var zipPath = Path.Combine(temp.FullName, "sample.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("folder/readme.txt");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("hello");
        }

        var result = await new PreviewService().CreateAsync([new BrowserItem(new FileInfo(zipPath))]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Kind, Is.EqualTo(PreviewKind.Archive));
            Assert.That(result.Content, Does.Contain("folder/readme.txt"));
            Assert.That(result.Details, Does.Contain("1 entries"));
        });
    }

    [TestCase("sample.cs")]
    [TestCase("sample.h")]
    [TestCase("sample.cpp")]
    public async Task CheckCommonSourceFilesUseSyntaxPreview(string name)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullName, name);
        await File.WriteAllTextAsync(path, "class Sample { }");

        var result = await new PreviewService().CreateAsync([new BrowserItem(new FileInfo(path))]);
        var highlighting = new CodeHighlightingConverter().Convert(path, typeof(object), null, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Kind, Is.EqualTo(PreviewKind.Code));
            Assert.That(highlighting, Is.Not.Null);
        });
    }

    [Test]
    public async Task CheckLargeTextPreviewIsBounded()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullName, "large.txt");
        await File.WriteAllLinesAsync(path, Enumerable.Range(0, 2_000).Select(index => $"Line {index:D4}: {new string('x', 100)}"));

        var result = await new PreviewService().CreateAsync([new BrowserItem(new FileInfo(path))]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Content, Does.EndWith("… preview truncated …"));
            Assert.That(result.Content.Split('\n').Length, Is.LessThanOrEqualTo(602));
            Assert.That(result.Content.Length, Is.LessThan(10_000));
        });
    }
}
