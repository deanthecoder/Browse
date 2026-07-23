// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using Browse.Services;
using Browse.ViewModels;
using Markdown.Avalonia;

namespace Browse.Views;

/// <summary>
/// Displays the current bounded preview in a larger independent window.
/// </summary>
/// <remarks>
/// The preview reuses the owning browser view model so selection changes can be reflected immediately.
/// </remarks>
public partial class PreviewWindow : Window
{
    private MainWindowViewModel m_viewModel;
    private bool m_updateQueued;

    public PreviewWindow()
    {
        InitializeComponent();
        Icon = IconLoader.LoadWindowIcon();
        Opened += OnOpened;
        Closed += OnClosed;
        DataContextChanged += OnDataContextChanged;
    }

    public PreviewWindow(MainWindowViewModel viewModel) : this() => DataContext = viewModel;

    private void OnOpenClicked(object sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.OpenSelected();

    private void OnOpened(object sender, EventArgs e)
    {
        PreviewHost.Content = CreateMessage("Loading preview…");
        QueuePreviewUpdate();
    }

    private void OnDataContextChanged(object sender, EventArgs e)
    {
        if (m_viewModel != null)
            m_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        m_viewModel = DataContext as MainWindowViewModel;
        if (m_viewModel != null)
            m_viewModel.PropertyChanged += OnViewModelPropertyChanged;
        if (IsVisible)
            QueuePreviewUpdate();
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.PreviewKind) or
            nameof(MainWindowViewModel.PreviewContent) or
            nameof(MainWindowViewModel.PreviewImage) or
            nameof(MainWindowViewModel.PreviewPath))
            QueuePreviewUpdate();
    }

    private void QueuePreviewUpdate()
    {
        if (m_updateQueued)
            return;
        m_updateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            m_updateQueued = false;
            if (IsVisible)
                PreviewHost.Content = CreatePreview();
        }, DispatcherPriority.Background);
    }

    private Control CreatePreview()
    {
        if (m_viewModel == null)
            return CreateMessage("No larger preview is available for this item.");
        if (m_viewModel.IsImagePreview)
            return new Image { Source = m_viewModel.PreviewImage, Stretch = Stretch.Uniform };
        if (m_viewModel.IsTextPreview)
        {
            return new TextBox
            {
                Text = m_viewModel.PreviewContent,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                Background = Brush.Parse("#111316"),
                BorderThickness = new Thickness(0)
            };
        }
        if (m_viewModel.IsCodePreview)
        {
            return new TextEditor
            {
                Document = new TextDocument(m_viewModel.PreviewContent ?? string.Empty),
                SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(m_viewModel.PreviewPath)),
                IsReadOnly = true,
                WordWrap = false,
                ShowLineNumbers = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Background = Brush.Parse("#111316")
            };
        }
        if (m_viewModel.IsMarkdownPreview)
            return new MarkdownScrollViewer { Markdown = m_viewModel.PreviewContent };
        if (m_viewModel.IsArchivePreview)
        {
            return new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new TextBlock
                {
                    Text = m_viewModel.PreviewContent,
                    TextWrapping = TextWrapping.NoWrap,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11
                }
            };
        }
        return CreateMessage("No larger preview is available for this item.");
    }

    private static TextBlock CreateMessage(string text) => new()
    {
        Text = text,
        Foreground = Brush.Parse("#9CA5B2"),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void OnClosed(object sender, EventArgs e)
    {
        if (m_viewModel != null)
            m_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        PreviewHost.Content = null;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
