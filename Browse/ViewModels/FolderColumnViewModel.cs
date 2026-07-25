// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.ObjectModel;
using Browse.Models;
using DTC.Core.ViewModels;

namespace Browse.ViewModels;

/// <summary>
/// Represents one folder column in the column browser.
/// </summary>
/// <remarks>
/// Each column owns only its visible snapshot and selection state.
/// </remarks>
public sealed class FolderColumnViewModel : ViewModelBase
{
    private bool m_isLoading;
    private string m_error;
    private bool m_isRefreshing;
    private readonly HashSet<string> m_selectedPaths = new(StringComparer.OrdinalIgnoreCase);

    public FolderColumnViewModel(DirectoryInfo directory)
    {
        Directory = directory;
        Title = string.IsNullOrWhiteSpace(directory.Name) ? directory.FullName : directory.Name;
    }

    public DirectoryInfo Directory { get; }
    public string Title { get; }
    public ObservableCollection<BrowserItem> Items { get; } = [];

    public void SetSelection(IEnumerable<BrowserItem> items)
        => SetSelectionPaths(items.Select(item => item.FullPath));

    public void SetSelectionPaths(IEnumerable<string> paths)
    {
        m_selectedPaths.Clear();
        foreach (var path in paths)
            m_selectedPaths.Add(path);
    }

    public string GetSelectionPathAfterRemoving(IEnumerable<BrowserItem> items)
    {
        var removedPaths = items
            .Select(item => item.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var firstRemovedIndex = -1;
        for (var index = 0; index < Items.Count; index++)
        {
            if (!removedPaths.Contains(Items[index].FullPath))
                continue;
            firstRemovedIndex = index;
            break;
        }
        if (firstRemovedIndex < 0)
            return null;

        var remainingItems = Items
            .Where(item => !removedPaths.Contains(item.FullPath))
            .ToArray();
        return remainingItems.Length == 0
            ? null
            : remainingItems[Math.Min(firstRemovedIndex, remainingItems.Length - 1)].FullPath;
    }

    public void ReplaceItems(IReadOnlyList<BrowserItem> items)
    {
        var existing = Items.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);
        var desired = items
            .Select(item => existing.TryGetValue(item.FullPath, out var current) &&
                            current.EffectiveExtension == item.EffectiveExtension &&
                            (m_selectedPaths.Contains(item.FullPath) || HasSameDisplayMetadata(current, item))
                ? current
                : item)
            .ToArray();
        var desiredPaths = desired
            .Select(item => item.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (!desiredPaths.Contains(Items[index].FullPath))
                Items.RemoveAt(index);
        }
        for (var index = 0; index < desired.Length; index++)
        {
            if (index < Items.Count && ReferenceEquals(Items[index], desired[index]))
                continue;
            if (index < Items.Count && string.Equals(Items[index].FullPath, desired[index].FullPath, StringComparison.OrdinalIgnoreCase))
            {
                Items[index] = desired[index];
                continue;
            }
            var existingIndex = Items.IndexOf(desired[index]);
            if (existingIndex >= 0)
                Items.Move(existingIndex, index);
            else
                Items.Insert(index, desired[index]);
        }
        while (Items.Count > desired.Length)
            Items.RemoveAt(Items.Count - 1);
    }

    private static bool HasSameDisplayMetadata(BrowserItem first, BrowserItem second) =>
        first.Name == second.Name &&
        first.IsDirectory == second.IsDirectory &&
        first.IsDotFolder == second.IsDotFolder &&
        first.IsHidden == second.IsHidden &&
        first.IsUnavailable == second.IsUnavailable &&
        first.LastWriteTime == second.LastWriteTime &&
        first.Size == second.Size &&
        first.EffectiveExtension == second.EffectiveExtension &&
        first.Icon == second.Icon;

    public bool IsSelectedPath(string path) => m_selectedPaths.Contains(path);

    public bool IsRefreshing
    {
        get => m_isRefreshing;
        set => SetField(ref m_isRefreshing, value);
    }

    public bool IsLoading
    {
        get => m_isLoading;
        set => SetField(ref m_isLoading, value);
    }

    public string Error
    {
        get => m_error;
        set => SetField(ref m_error, value);
    }
}
