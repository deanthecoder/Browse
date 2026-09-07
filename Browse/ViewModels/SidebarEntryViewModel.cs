// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.Extensions;

namespace Browse.ViewModels;

/// <summary>
/// Describes a favorite or drive shown in the sidebar.
/// </summary>
/// <remarks>
/// Sidebar entries retain a plain path so they can represent local and UNC roots alike.
/// </remarks>
public sealed record SidebarEntryViewModel(string Name, string Path, bool IsDrive = false)
{
    /// <summary>Reads current capacity information for a drive tooltip.</summary>
    /// <remarks>Call off the UI thread because removable and network drives can respond slowly.</remarks>
    public string GetDriveToolTip()
    {
        try
        {
            var drive = new DriveInfo(Path);
            return drive.IsReady
                ? FormatDriveUsage(Path, drive.TotalSize, drive.TotalFreeSpace)
                : $"{Path}\nDisk usage unavailable";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return $"{Path}\nDisk usage unavailable";
        }
    }

    internal static string FormatDriveUsage(string path, long total, long free)
    {
        if (total <= 0 || free < 0 || free > total)
            return $"{path}\nDisk usage unavailable";
        var used = total - free;
        return $"{path}\n{used.ToSize()} used of {total.ToSize()} ({used / (double)total:P0})\n{free.ToSize()} free";
    }
}
