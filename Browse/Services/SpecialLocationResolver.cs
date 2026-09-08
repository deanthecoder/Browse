// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.


using System.Runtime.InteropServices;

namespace Browse.Services;

public static class SpecialLocationResolver
{
    // Resolve only the first component, leaving absolute paths and ./relative paths alone.
    public static string Expand(string path)
    {
        var separator = path.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        var name = separator < 0 ? path : path[..separator];
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var location = name.ToLowerInvariant() switch
        {
            "home" => home,
            "temp" => Path.GetTempPath(),
            "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "downloads" => OperatingSystem.IsWindows()
                ? GetWindowsFolder(new Guid("374DE290-123F-4565-9164-39C4925E467B"))
                : string.IsNullOrEmpty(home) ? string.Empty : Path.Combine(home, "Downloads"),
            "public" => OperatingSystem.IsWindows()
                ? GetWindowsFolder(new Guid("DFDF76A2-C82A-4D63-906A-5644AC457385"))
                : string.IsNullOrEmpty(home) ? string.Empty : Path.Combine(home, "Public"),
            "appdata" => OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : string.Empty,
            "localappdata" => OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) : string.Empty,
            "programdata" => OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) : string.Empty,
            _ => null
        };
        if (location == null)
            return path;
        // An unavailable alias must not accidentally resolve relative to the working directory.
        if (string.IsNullOrEmpty(location))
            return string.Empty;
        return separator < 0 ? location : Path.Join(location, path[(separator + 1)..]);
    }

    private static string GetWindowsFolder(Guid folderId)
    {
        var result = SHGetKnownFolderPath(ref folderId, 0, IntPtr.Zero, out var pointer);
        try
        {
            return result < 0 ? string.Empty : Marshal.PtrToStringUni(pointer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);
}

