// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Browse.Models;

public enum PreviewKind
{
    None,
    Folder,
    Text,
    Code,
    Markdown,
    Archive,
    Image,
    Multiple
}

/// <summary>
/// Contains bounded preview content for the Info panel.
/// </summary>
/// <remarks>
/// Preview services never place an entire unbounded file in this object.
/// </remarks>
public sealed record PreviewResult(
    PreviewKind Kind,
    string Name,
    string Path = null,
    string Details = null,
    string Content = null,
    string ImagePath = null);
