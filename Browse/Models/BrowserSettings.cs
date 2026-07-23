// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.Settings;

namespace Browse.Models;

/// <summary>
/// Stores user-configurable Browse behavior.
/// </summary>
/// <remarks>
/// Settings are intentionally compact and portable between supported desktop platforms.
/// </remarks>
public sealed class BrowserSettings : UserSettingsBase
{
    public bool ShowHiddenItems
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowDotFolders
    {
        get => Get<bool>();
        set => Set(value);
    }

    public string DefaultPath
    {
        get => Get<string>();
        set => Set(value);
    }

    public string TerminalCommand
    {
        get => Get<string>();
        set => Set(value);
    }

    public bool EnableGlobalShortcut
    {
        get => Get<bool>();
        set => Set(value);
    }

    public string[] FavoritePaths
    {
        get => Get<string[]>();
        set => Set(value);
    }

    protected override void ApplyDefaults()
    {
        DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        EnableGlobalShortcut = true;
    }
}
