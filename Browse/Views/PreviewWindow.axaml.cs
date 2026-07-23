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
using Avalonia.Input;
using Avalonia.Interactivity;
using Browse.Services;
using Browse.ViewModels;

namespace Browse.Views;

/// <summary>
/// Displays the current bounded preview in a larger independent window.
/// </summary>
/// <remarks>
/// The preview reuses the owning browser view model so selection changes can be reflected immediately.
/// </remarks>
public partial class PreviewWindow : Window
{
    public PreviewWindow()
    {
        InitializeComponent();
        Icon = IconLoader.LoadWindowIcon();
    }

    public PreviewWindow(MainWindowViewModel viewModel) : this() => DataContext = viewModel;

    private void OnOpenClicked(object sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.OpenSelected();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
