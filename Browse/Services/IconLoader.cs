// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Browse.Services;

/// <summary>
/// Loads the packaged Browse application icon.
/// </summary>
/// <remarks>
/// A shared loader keeps window, tray, and dialog branding consistent.
/// </remarks>
public static class IconLoader
{
    public static WindowIcon LoadWindowIcon() =>
        new(AssetLoader.Open(new Uri("avares://Browse/Assets/app.ico")));

    public static Bitmap LoadBitmap() =>
        new(AssetLoader.Open(new Uri("avares://Browse/Assets/app.png")));
}
