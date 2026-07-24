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
    public async Task CheckExpandedTextReadsTheCompleteFile()
    {
        var path = Path.GetTempFileName();
        var text = string.Join('\n', Enumerable.Range(1, 2_000).Select(i => $"Line {i:N0}: expanded preview content"));
        await File.WriteAllTextAsync(path, text);
        try
        {
            var preview = new TextPreviewContent(
                Path.GetFileName(path),
                path,
                null,
                text[..8_192] + "\n\n… preview truncated …",
                TextPreviewMode.Code,
                "txt",
                true);

            var expanded = await PreviewWindow.ReadExpandedTextAsync(preview, CancellationToken.None);

            Assert.That(expanded, Is.EqualTo(text));
            Assert.That(expanded, Does.Not.Contain("preview truncated"));
        }
        finally
        {
            File.Delete(path);
        }
    }

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

    [Test]
    public async Task CheckCodePreviewFallsBackToPlainEditorWhenColoringTimesOut()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        await session.Dispatch(() =>
        {
            var json = "{\"values\":[" + string.Join(',', Enumerable.Repeat("{\"value\":true}", 100_000)) + "]}";
            var preview = new TextPreviewContent(
                "large.json",
                "/tmp/large.json",
                null,
                json,
                TextPreviewMode.Code,
                "json");
            var colorizer = TextMateCodeColorizer.Create(
                preview.Path,
                preview.Text,
                TimeSpan.FromMilliseconds(25));

            var editor = PreviewWindow.CreateCodePreview(preview, preview.Text, colorizer);

            Assert.That(colorizer, Is.Null);
            Assert.That(editor.TextArea.TextView.LineTransformers, Has.None.TypeOf<TextMateCodeColorizer>());
            Assert.That(editor.Text, Is.EqualTo(preview.Text));
            return true;
        }, CancellationToken.None);
    }
}
