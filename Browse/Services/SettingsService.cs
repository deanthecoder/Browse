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

namespace Browse.Services;

/// <summary>
/// Persists Browse settings in the user's application-data folder.
/// </summary>
/// <remarks>
/// Failed reads fall back to defaults; a damaged settings file must never prevent startup.
/// </remarks>
public sealed class SettingsService
{
    private BrowserSettings m_settings;

    public BrowserSettings Load() => m_settings ??= new BrowserSettings();

    public void Save(BrowserSettings settings)
    {
        m_settings = settings;
        settings.Save();
    }
}
