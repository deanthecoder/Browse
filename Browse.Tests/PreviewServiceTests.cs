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
using System.Reflection;
using System.Text;
using Avalonia.Headless;
using Browse.Models;
using Browse.Services;
using Browse.Services.Previews;
using Browse.Services.Thumbnails;
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
            Assert.That(result.Language, Is.EqualTo("md"));
        });
    }

    [Test]
    public async Task CheckAliasedSourceFileUsesTargetGrammar()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullName, "sample.slnx");
        await File.WriteAllTextAsync(path, "<Solution />");
        var item = new BrowserItem(new FileInfo(path), new FileTypeAliasMap([".slnx=.xml"]));

        var result = (TextPreviewContent)await new PreviewService().CreateAsync([item]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Mode, Is.EqualTo(TextPreviewMode.Code));
            Assert.That(result.Language, Is.EqualTo("xml"));
        });
    }

    [Test]
    public async Task CheckAliasedZipUsesArchivePreview()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullName, "sample.workzip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            archive.CreateEntry("inside.txt").Open().Dispose();
        var item = new BrowserItem(new FileInfo(path), new FileTypeAliasMap([".workzip=.zip"]));

        var result = await new PreviewService().CreateAsync([item]);

        Assert.That(result, Is.TypeOf<ArchivePreviewContent>());
        Assert.That(((ArchivePreviewContent)result).Text, Does.Contain("inside.txt"));
    }

    [Test]
    public async Task CheckPdfPreviewRendersFirstPage()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, "sample.pdf"));
        WriteSamplePdf(file);

        using var result = await session.Dispatch(
            () => new PreviewService().CreateAsync([new BrowserItem(file)]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ImagePreviewContent>());
            Assert.That(result.Details, Does.Contain("1 page"));
            Assert.That(((ImagePreviewContent)result).Image.PixelSize.Width, Is.GreaterThan(0));
            Assert.That(((ImagePreviewContent)result).Image.PixelSize.Height, Is.GreaterThan(0));
        });
    }

    [TestCase("sample.mp4", true)]
    [TestCase("sample.avi", true)]
    [TestCase("sample.mov", true)]
    [TestCase("sample.txt", false)]
    public async Task CheckCommonVideoExtensionsUseNativeThumbnailPreview(string name, bool expected)
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, name));
        await File.WriteAllTextAsync(file.FullName, "content");
        var provider = new VideoPreviewProvider(new PlatformThumbnailService(new EmptyThumbnailProvider()));

        var result = await provider.CanPreviewAsync(new BrowserItem(file), CancellationToken.None);

        Assert.That(result, Is.EqualTo(expected));
    }

    private static void WriteSamplePdf(FileInfo file)
    {
        const string pageContent = "BT /F1 18 Tf 20 50 Td (Browse PDF preview) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {pageContent.Length} >>\nstream\n{pageContent}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var crossReferenceOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{crossReferenceOffset}\n%%EOF\n");
        File.WriteAllText(file.FullName, pdf.ToString(), Encoding.ASCII);
    }

    [TestCase("sample.html")]
    [TestCase("sample.htm")]
    public async Task CheckHtmlUsesRenderedExpandedPreview(string name)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullName, name);
        await File.WriteAllTextAsync(path, "<h1>Heading</h1>");

        var result = (TextPreviewContent)await new PreviewService().CreateAsync([new BrowserItem(new FileInfo(path))]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Mode, Is.EqualTo(TextPreviewMode.Html));
            Assert.That(result.Text, Does.Contain("<h1>Heading</h1>"));
            Assert.That(result.CanExpand, Is.True);
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
            var textPreview = (TextPreviewContent)result;
            Assert.That(textPreview.Text, Does.EndWith("… preview truncated …"));
            Assert.That(textPreview.Text.Split('\n').Length, Is.LessThanOrEqualTo(602));
            Assert.That(textPreview.Text.Length, Is.LessThan(10_000));
            Assert.That(textPreview.IsTruncated, Is.True);
        });
    }

    [TestCase("sample.exe", true)]
    [TestCase("sample.dll", true)]
    [TestCase("sample.com", false)]
    public async Task CheckExecutablePreviewExtensions(string name, bool expected)
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, name));
        await File.WriteAllTextAsync(file.FullName, "content");

        var result = await new ExecutablePreviewProvider().CanPreviewAsync(new BrowserItem(file), CancellationToken.None);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public async Task CheckExecutablePreviewShowsVersionMetadata()
    {
        var file = new FileInfo(typeof(PreviewService).Assembly.Location);

        var result = await new PreviewService().CreateAsync([new BrowserItem(file)]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<TextPreviewContent>());
            Assert.That(((TextPreviewContent)result).Text, Does.Contain("File version: 0.1.0.0"));
            Assert.That(((TextPreviewContent)result).Text, Does.Contain("Product version: 0.1"));
            Assert.That(((TextPreviewContent)result).Text, Does.Contain("Digital signature:"));
            Assert.That(result.CanExpand, Is.False);
        });
    }

    private sealed class StubPreviewProvider(bool matches, PreviewContent content) : IPreviewProvider
    {
        public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
            ValueTask.FromResult(matches);

        public Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken) =>
            Task.FromResult(content);
    }

    private sealed class EmptyThumbnailProvider : IPlatformThumbnailProvider
    {
        public Task<Avalonia.Media.Imaging.Bitmap> CreateAsync(
            FileInfo file,
            int maximumDimension,
            CancellationToken cancellationToken) => Task.FromResult<Avalonia.Media.Imaging.Bitmap>(null);
    }
}
