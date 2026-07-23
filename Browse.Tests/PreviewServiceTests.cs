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
using Browse.Models;
using Browse.Services;
using Browse.Services.Previews;
using DTC.Core;

namespace Browse.Tests;

[TestFixture]
public sealed class PreviewServiceTests
{
    [Test]
    public async Task CheckFirstMatchingProviderCreatesPreview()
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "sample.bin"));
        await File.WriteAllTextAsync(file.FullName, "content");
        var expected = new EmptyPreviewContent("Custom preview");
        var service = new PreviewService([
            new StubPreviewProvider(false, new EmptyPreviewContent("Wrong preview")),
            new StubPreviewProvider(true, expected)
        ]);

        var result = await service.CreateAsync([new BrowserItem(file)]);

        Assert.That(result, Is.SameAs(expected));
    }

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
            Assert.That(result, Is.TypeOf<ArchivePreviewContent>());
            Assert.That(((ArchivePreviewContent)result).Text, Does.Contain("folder/readme.txt"));
            Assert.That(result.Details, Does.Contain("1 entries"));
        });
    }

    [TestCase("sample.cs")]
    [TestCase("sample.h")]
    [TestCase("sample.cpp")]
    [TestCase("sample.xml")]
    [TestCase("sample.json")]
    [TestCase("sample.ps1")]
    public async Task CheckCommonSourceFilesUseSyntaxPreview(string name)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullName, name);
        await File.WriteAllTextAsync(path, "class Sample { }");

        var result = await new PreviewService().CreateAsync([new BrowserItem(new FileInfo(path))]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<TextPreviewContent>());
            Assert.That(((TextPreviewContent)result).Mode, Is.EqualTo(TextPreviewMode.Code));
            Assert.That(((TextPreviewContent)result).Language, Is.EqualTo(Path.GetExtension(path).TrimStart('.')));
        });
    }

    [Test]
    public async Task CheckMarkdownUsesMarkdownPreviewInsteadOfCodeGrammar()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullName, "readme.md");
        await File.WriteAllTextAsync(path, "# Heading");

        var result = (TextPreviewContent)await new PreviewService().CreateAsync([new BrowserItem(new FileInfo(path))]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Mode, Is.EqualTo(TextPreviewMode.Markdown));
            Assert.That(result.Language, Is.Null);
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
            Assert.That(result, Is.TypeOf<TextPreviewContent>());
            Assert.That(((TextPreviewContent)result).Text, Does.EndWith("… preview truncated …"));
            Assert.That(((TextPreviewContent)result).Text.Split('\n').Length, Is.LessThanOrEqualTo(602));
            Assert.That(((TextPreviewContent)result).Text.Length, Is.LessThan(10_000));
        });
    }

    private sealed class StubPreviewProvider(bool matches, PreviewContent content) : IPreviewProvider
    {
        public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
            ValueTask.FromResult(matches);

        public Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken) =>
            Task.FromResult(content);
    }
}
