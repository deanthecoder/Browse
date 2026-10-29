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
using DTC.Core.Extensions;
using DTC.Core.UI;

namespace Browse.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    public AboutWindow(AboutInfo info) : this() => DataContext = info;

    private void OnWebsiteClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is AboutInfo { WebsiteUrl: { Length: > 0 } url })
            new Uri(url).Open();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        e.Handled = true;
        Close();
    }
}
