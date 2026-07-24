// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Reflection;
using Avalonia.Headless;
using AvaloniaEdit;
using Browse.Models;
using Browse.Views;

namespace Browse.Tests;

[TestFixture]
public sealed class PreviewWindowTests
{
    [Test]
    public async Task CheckCodePreviewUsesViewportColorizer()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        await session.Dispatch(() =>
        {
            var preview = new TextPreviewContent(
                "sample.json",
                "/tmp/sample.json",
                null,
                "{\n  \"enabled\": true\n}",
                TextPreviewMode.Code,
                "json");

            var editor = PreviewWindow.CreateCodePreview(preview);

            Assert.That(editor, Is.TypeOf<TextEditor>());
            Assert.That(editor.IsReadOnly, Is.True);
            Assert.That(editor.TextArea.TextView.LineTransformers, Has.Exactly(1).TypeOf<TextMateCodeColorizer>());
            return true;
        }, CancellationToken.None);
    }
}
