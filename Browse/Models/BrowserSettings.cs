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

/// <summary>
/// Stores user-configurable Browse behavior.
/// </summary>
/// <remarks>
/// Settings are intentionally compact and portable between supported desktop platforms.
/// </remarks>
public sealed class BrowserSettings
{
    public bool ShowHiddenItems { get; set; }
    public bool ShowDotFolders { get; set; }
    public string DefaultPath { get; set; }
    public string TerminalCommand { get; set; }
    public bool EnableGlobalShortcut { get; set; } = true;
}
