// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Browse.ViewModels;

/// <summary>
/// Describes a favorite or drive shown in the sidebar.
/// </summary>
/// <remarks>
/// Sidebar entries retain a plain path so they can represent local and UNC roots alike.
/// </remarks>
public sealed record SidebarEntryViewModel(string Name, string Path, bool IsDrive = false);
