// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Text.Json;
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
    private readonly FileInfo m_settingsFile;

    public SettingsService(FileInfo settingsFile = null)
    {
        m_settingsFile = settingsFile ?? new FileInfo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Browse",
            "settings.json"));
    }

    public BrowserSettings Load()
    {
        try
        {
            if (m_settingsFile.Exists)
                return JsonSerializer.Deserialize<BrowserSettings>(File.ReadAllText(m_settingsFile.FullName)) ?? CreateDefaults();
        }
        catch (Exception) when (m_settingsFile.Exists)
        {
            // Keep a damaged or inaccessible settings file from preventing startup.
        }
        return CreateDefaults();
    }

    public void Save(BrowserSettings settings)
    {
        m_settingsFile.Directory?.Create();
        File.WriteAllText(m_settingsFile.FullName, JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static BrowserSettings CreateDefaults() => new()
    {
        DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
    };
}
