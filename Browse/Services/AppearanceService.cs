// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using DTC.AsciiTheme;

namespace Browse.Services;

/// <summary>
/// Applies the saved appearance before Browse creates its windows.
/// </summary>
internal sealed class AppearanceService(Application application)
{
    private AsciiTheme m_retroTheme;
    private ResourceInclude m_palette;
    private bool? m_isRetro;

    // Replacing ScrollViewer templates on a live visual tree is unsafe.
    // Only apply an appearance at startup, before any windows are created.
    public void Apply(bool retro)
    {
        if (m_isRetro == retro)
            return;

        var palette = new ResourceInclude(new Uri("avares://Browse/"))
        {
            Source = new Uri($"avares://Browse/Styles/{(retro ? "Retro" : "Modern")}Palette.axaml")
        };
        if (m_palette == null)
            application.Resources.MergedDictionaries.Add(palette);
        else
            application.Resources.MergedDictionaries[
                application.Resources.MergedDictionaries.IndexOf(m_palette)] = palette;
        m_palette = palette;

        if (retro)
        {
            m_retroTheme ??= new AsciiTheme();
            // Keep Browse's layout and selection styles after the control theme.
            application.Styles.Insert(application.Styles.Count - 1, m_retroTheme);
        }
        else if (m_retroTheme != null)
            application.Styles.Remove(m_retroTheme);

        m_isRetro = retro;
    }
}
