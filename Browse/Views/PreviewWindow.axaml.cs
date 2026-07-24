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
using Browse.Models;
using Browse.Services;
using Browse.ViewModels;
using LiveMarkdown.Avalonia;
using TextMateSharp.Grammars;

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
    private CancellationTokenSource m_previewCancellation = new();
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
        if (e.PropertyName == nameof(MainWindowViewModel.Preview))
            QueuePreviewUpdate();
    }

    private void QueuePreviewUpdate()
    {
        m_previewCancellation.Cancel();
        m_previewCancellation.Dispose();
        m_previewCancellation = new CancellationTokenSource();
        if (m_updateQueued)
            return;
        m_updateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            m_updateQueued = false;
            if (IsVisible)
                _ = UpdatePreviewAsync(m_previewCancellation.Token);
        }, DispatcherPriority.Background);
    }

    private async Task UpdatePreviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            var preview = await CreatePreviewAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsVisible)
                PreviewHost.Content = preview;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (IsVisible)
                PreviewHost.Content = CreateMessage($"Preview unavailable · {ex.Message}");
        }
    }

    private async Task<Control> CreatePreviewAsync(CancellationToken cancellationToken)
    {
        if (m_viewModel == null)
            return CreateMessage("No larger preview is available for this item.");
        if (m_viewModel.Preview is ImagePreviewContent image)
            return new Image { Source = image.Image, Stretch = Stretch.Uniform };
        if (m_viewModel.Preview is TextPreviewContent { Mode: TextPreviewMode.Plain } plainText)
        {
            var text = await ReadExpandedTextAsync(plainText, cancellationToken);
            return new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                Background = Brush.Parse("#111316"),
                BorderThickness = new Thickness(0)
            };
        }
        if (m_viewModel.Preview is TextPreviewContent { Mode: TextPreviewMode.Code } code)
        {
            var text = await ReadExpandedTextAsync(code, cancellationToken);
            var colorizer = await Task.Run(() => TextMateCodeColorizer.Create(code.Path, text), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return CreateCodePreview(code, text, colorizer);
        }
        if (m_viewModel.Preview is TextPreviewContent { Mode: TextPreviewMode.Markdown } markdown)
        {
            var text = await ReadExpandedTextAsync(markdown, cancellationToken);
            return new ScrollViewer
            {
                Content = new MarkdownRenderer
                {
                    MarkdownBuilder = new ObservableStringBuilder(text),
                    ImageBasePath = Path.GetDirectoryName(markdown.Path),
                    CodeBlockColorTheme = ThemeName.AtomOneDark
                }
            };
        }
        if (m_viewModel.Preview is ArchivePreviewContent archive)
        {
            return new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new TextBlock
                {
                    Text = archive.Text,
                    TextWrapping = TextWrapping.NoWrap,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11
                }
            };
        }
        return CreateMessage("No larger preview is available for this item.");
    }

    internal static TextEditor CreateCodePreview(TextPreviewContent code) =>
        CreateCodePreview(code, code.Text, TextMateCodeColorizer.Create(code.Path, code.Text));

    private static TextEditor CreateCodePreview(TextPreviewContent code, string text, TextMateCodeColorizer colorizer)
    {
        var editor = new TextEditor
        {
            Document = new TextDocument(text),
            IsReadOnly = true,
            WordWrap = false,
            ShowLineNumbers = true,
            FontFamily = new FontFamily("Cascadia Code,Consolas,Menlo,Monospace"),
            FontSize = 13,
            Background = Brush.Parse("#1E1E1E"),
            Foreground = Brush.Parse("#D4D4D4")
        };
        if (colorizer != null)
            editor.TextArea.TextView.LineTransformers.Add(colorizer);
        return editor;
    }

    internal static async Task<string> ReadExpandedTextAsync(TextPreviewContent preview, CancellationToken cancellationToken) =>
        preview.IsTruncated
            ? await File.ReadAllTextAsync(preview.Path, cancellationToken)
            : preview.Text;

    private static TextBlock CreateMessage(string text) => new()
    {
        Text = text,
        Foreground = Brush.Parse("#9CA5B2"),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void OnClosed(object sender, EventArgs e)
    {
        m_previewCancellation.Cancel();
        m_previewCancellation.Dispose();
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
