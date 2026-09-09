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
using DTC.Core.ViewModels;

namespace Browse.ViewModels;

/// <summary>
/// Supplies asynchronously loaded capacity details for a drive tooltip.
/// </summary>
/// <remarks>
/// Drive access runs off the UI thread because removable and network drives may respond slowly.
/// </remarks>
public sealed class DriveToolTipViewModel : ViewModelBase
{
    private readonly Func<DriveUsage> m_readUsage;
    private Task m_refreshTask;
    private bool m_isLoading;
    private bool m_isAvailable;
    private double m_usedPercentage;
    private string m_usageText;
    private string m_freeText;

    public DriveToolTipViewModel(string name, string path) : this(name, path, () => ReadUsage(path))
    {
    }

    internal DriveToolTipViewModel(string name, string path, Func<DriveUsage> readUsage)
    {
        Name = name;
        Path = path;
        m_readUsage = readUsage;
    }

    public string Name { get; }
    public string Path { get; }
    public bool IsLoading
    {
        get => m_isLoading;
        private set
        {
            if (SetField(ref m_isLoading, value))
                OnPropertyChanged(nameof(IsUnavailable));
        }
    }
    public bool IsAvailable
    {
        get => m_isAvailable;
        private set
        {
            if (SetField(ref m_isAvailable, value))
                OnPropertyChanged(nameof(IsUnavailable));
        }
    }
    public bool IsUnavailable => !IsLoading && !IsAvailable;
    public double UsedPercentage
    {
        get => m_usedPercentage;
        private set => SetField(ref m_usedPercentage, value);
    }
    public string UsageText
    {
        get => m_usageText;
        private set => SetField(ref m_usageText, value);
    }
    public string FreeText
    {
        get => m_freeText;
        private set => SetField(ref m_freeText, value);
    }

    public Task RefreshAsync() => m_refreshTask is { IsCompleted: false } ? m_refreshTask : m_refreshTask = RefreshCoreAsync();

    private async Task RefreshCoreAsync()
    {
        IsLoading = true;
        try
        {
            Apply(await Task.Run(m_readUsage));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Apply(DriveUsage.Unavailable);
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal void Apply(DriveUsage usage)
    {
        IsAvailable = usage.IsAvailable;
        UsedPercentage = usage.IsAvailable ? (usage.TotalBytes - usage.FreeBytes) * 100.0 / usage.TotalBytes : 0;
        UsageText = usage.IsAvailable
            ? $"{(usage.TotalBytes - usage.FreeBytes).ToSize()} used of {usage.TotalBytes.ToSize()}"
            : "Disk usage unavailable";
        FreeText = usage.IsAvailable ? $"{usage.FreeBytes.ToSize()} free" : null;
    }

    private static DriveUsage ReadUsage(string path)
    {
        var drive = new DriveInfo(path);
        return drive.IsReady && drive.TotalSize > 0 && drive.TotalFreeSpace >= 0 && drive.TotalFreeSpace <= drive.TotalSize
            ? new DriveUsage(drive.TotalSize, drive.TotalFreeSpace)
            : DriveUsage.Unavailable;
    }

    internal readonly record struct DriveUsage(long TotalBytes, long FreeBytes)
    {
        public static DriveUsage Unavailable => new(0, 0);
        public bool IsAvailable => TotalBytes > 0 && FreeBytes >= 0 && FreeBytes <= TotalBytes;
    }
}
