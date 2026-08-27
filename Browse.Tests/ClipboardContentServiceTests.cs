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
public sealed class ClipboardContentServiceTests
{
    [TestCase("notes.data", "Plain text content.", "Text")]
    [TestCase("vector.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" />", "Text")]
    [TestCase("image.png", "\0\u0001\u0002\u0003", "Image")]
    [TestCase("document.pdf", "%PDF printable test content", "PdfFirstPage")]
    [TestCase("unknown.bin", "\0\u0001\u0002\u0003", null)]
    public async Task CheckFileTypesChooseExpectedClipboardContent(
        string name,
        string content,
        string expected)
    {
        using var temp = new TempDirectory();
        var file = new FileInfo(Path.Combine(temp.FullName, name));
        await File.WriteAllTextAsync(file.FullName, content);

        Assert.That(GetKind(file)?.ToString(), Is.EqualTo(expected));
    }

    private static ClipboardContentKind? GetKind(FileInfo file) =>
        ClipboardContentService.GetContentKind(new BrowserItem(file));
}
