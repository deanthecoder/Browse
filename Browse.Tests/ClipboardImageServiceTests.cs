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

namespace Browse.Tests;

[TestFixture]
public sealed class ClipboardImageServiceTests
{
    [TestCase("image.png", true)]
    [TestCase("image.tiff", true)]
    [TestCase("vector.svg", false)]
    [TestCase("notes.txt", false)]
    public void CheckOnlyDecodableImagesCanBeCopied(string name, bool expected)
    {
        Assert.That(ClipboardImageService.CanCopy(new BrowserItem(new FileInfo(name))), Is.EqualTo(expected));
    }
}
