// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Media.Imaging;

namespace Browse.Models;

/// <summary>
/// Contains the shared metadata for one bounded preview.
/// </summary>
/// <remarks>
/// Concrete content types let preview providers and renderers evolve independently.
/// </remarks>
public abstract class PreviewContent(string name, string path = null, string details = null) : IDisposable
{
    public string Name { get; } = name;
    public string Path { get; } = path;
    public string Details { get; } = details;
    public virtual bool CanExpand => false;
    public virtual void Dispose()
    {
    }
}

/// <summary>
/// Represents an empty or unsupported preview.
/// </summary>
/// <remarks>
/// A distinct type avoids null preview state throughout the UI.
/// </remarks>
public sealed class EmptyPreviewContent(string name = "No selection", string path = null, string details = null)
    : PreviewContent(name, path, details);

/// <summary>
/// Summarizes a multiple-item selection.
/// </summary>
/// <remarks>
/// Multiple selections intentionally avoid expensive per-item preview work.
/// </remarks>
public sealed class MultiplePreviewContent(string name, string details)
    : PreviewContent(name, details: details);

/// <summary>
/// Describes a selected folder.
/// </summary>
/// <remarks>
/// Folder size remains opt-in because recursive enumeration can be expensive.
/// </remarks>
public sealed class FolderPreviewContent(string name, string path, string details)
    : PreviewContent(name, path, details);

/// <summary>
/// Selects the renderer appropriate for bounded text content.
/// </summary>
public enum TextPreviewMode
{
    Plain,
    Code,
    Markdown
}

/// <summary>
/// Contains a bounded sample from a text-based file.
/// </summary>
/// <remarks>
/// The mode selects presentation without coupling providers to Avalonia controls.
/// </remarks>
public sealed class TextPreviewContent(
    string name,
    string path,
    string details,
    string text,
    TextPreviewMode mode) : PreviewContent(name, path, details)
{
    public string Text { get; } = text;
    public TextPreviewMode Mode { get; } = mode;
    public override bool CanExpand => true;
}

/// <summary>
/// Contains a bounded textual archive listing.
/// </summary>
/// <remarks>
/// Archive entries are represented as text to keep preview rendering lightweight.
/// </remarks>
public sealed class ArchivePreviewContent(string name, string path, string details, string text)
    : PreviewContent(name, path, details)
{
    public string Text { get; } = text;
    public override bool CanExpand => true;
}

/// <summary>
/// Owns a decoded, size-bounded image preview.
/// </summary>
/// <remarks>
/// Disposing the content deterministically releases native bitmap memory.
/// </remarks>
public sealed class ImagePreviewContent(
    string name,
    string path,
    string details,
    Bitmap image) : PreviewContent(name, path, details)
{
    public Bitmap Image { get; } = image;
    public override bool CanExpand => true;
    public override void Dispose() => Image.Dispose();
}
