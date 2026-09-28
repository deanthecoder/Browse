// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Browse.Services.Previews;

namespace Browse.Views;

/// <summary>
/// Pages through a PDF while retaining only the current rendered page.
/// </summary>
internal sealed class PdfPreviewViewer : UserControl, IDisposable
{
    private readonly FileInfo m_file;
    private readonly CancellationTokenSource m_cancellation;
    private readonly ContentControl m_host = new();
    private readonly Button m_previous = new() { Content = "Previous" };
    private readonly Button m_next = new() { Content = "Next" };
    private readonly TextBlock m_pageLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private bool m_loading;
    private bool m_disposed;

    internal int PageIndex { get; private set; }
    internal int PageCount { get; private set; }

    private PdfPreviewViewer(FileInfo file, RenderedPdfPage firstPage, CancellationToken cancellationToken)
    {
        m_file = file;
        m_cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { m_previous, m_pageLabel, m_next }
        };
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        layout.Children.Add(controls);
        Grid.SetRow(m_host, 1);
        layout.Children.Add(m_host);
        Content = layout;
        m_previous.Click += async (_, _) => await NavigateAsync(PageIndex - 1);
        m_next.Click += async (_, _) => await NavigateAsync(PageIndex + 1);
        SetPage(firstPage, 0);
    }

    internal static async Task<PdfPreviewViewer> CreateAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var firstPage = await PdfPreviewProvider.RenderPageAsync(file, 0, 1800, 96, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            firstPage.Bitmap?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (firstPage.Bitmap == null)
            throw new InvalidDataException("The PDF contains no pages.");
        return new PdfPreviewViewer(file, firstPage, cancellationToken);
    }

    private async Task NavigateAsync(int pageIndex)
    {
        try
        {
            await ShowPageAsync(pageIndex);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (!m_disposed)
                m_pageLabel.Text = $"Page {pageIndex + 1:N0} unavailable · Showing {PageIndex + 1:N0} of {PageCount:N0}";
        }
    }

    internal async Task ShowPageAsync(int pageIndex)
    {
        if (m_disposed || m_loading || pageIndex < 0 || pageIndex >= PageCount || pageIndex == PageIndex)
            return;
        m_loading = true;
        UpdateControls();
        m_pageLabel.Text = $"Loading page {pageIndex + 1:N0}…";
        var cancellationToken = m_cancellation.Token;
        try
        {
            var page = await PdfPreviewProvider.RenderPageAsync(m_file, pageIndex, 1800, 96, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                page.Bitmap?.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (page.Bitmap == null)
                throw new InvalidDataException("The PDF contains no pages.");
            SetPage(page, pageIndex);
        }
        finally
        {
            m_loading = false;
            if (!m_disposed)
                UpdateControls();
        }
    }

    private void SetPage(RenderedPdfPage page, int pageIndex)
    {
        (m_host.Content as IDisposable)?.Dispose();
        m_host.Content = new ImagePreviewViewer(page.Bitmap, true);
        PageIndex = pageIndex;
        PageCount = page.Result.PageCount;
        m_pageLabel.Text = $"Page {PageIndex + 1:N0} of {PageCount:N0}";
        UpdateControls();
    }

    private void UpdateControls()
    {
        m_previous.IsEnabled = !m_loading && PageIndex > 0;
        m_next.IsEnabled = !m_loading && PageIndex + 1 < PageCount;
    }

    public void Dispose()
    {
        if (m_disposed)
            return;
        m_disposed = true;
        m_cancellation.Cancel();
        m_cancellation.Dispose();
        (m_host.Content as IDisposable)?.Dispose();
        m_host.Content = null;
    }
}
