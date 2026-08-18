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
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Browse.Views;

/// <summary>
/// Displays an image with pointer-centered wheel zoom, drag panning, fit, and original-size controls.
/// </summary>
internal sealed class ImagePreviewViewer : UserControl, IDisposable
{
    private const double MinimumZoom = 0.05;
    private const double MaximumZoom = 32.0;
    private readonly Bitmap m_bitmap;
    private readonly bool m_ownsBitmap;
    private readonly ScrollViewer m_scrollViewer;
    private readonly Image m_image;
    private readonly TextBlock m_zoomLabel;
    private double m_zoom = 1;
    private bool m_fitToWindow = true;
    private double? m_pinchStartZoom;
    private Point? m_panStart;
    private Vector m_panStartOffset;

    public ImagePreviewViewer(Bitmap bitmap, bool ownsBitmap)
    {
        m_bitmap = bitmap;
        m_ownsBitmap = ownsBitmap;
        m_image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        m_image.PointerPressed += OnPointerPressed;
        m_image.PointerMoved += OnPointerMoved;
        m_image.PointerReleased += OnPointerReleased;
        m_image.PointerCaptureLost += (_, _) => m_panStart = null;

        m_scrollViewer = new ScrollViewer
        {
            Content = m_image,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AllowAutoHide = false
        };
        m_scrollViewer.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        Gestures.AddPointerTouchPadGestureMagnifyHandler(m_scrollViewer, OnTouchPadMagnify);
        m_scrollViewer.GestureRecognizers.Add(new PinchGestureRecognizer());
        Gestures.AddPinchHandler(m_scrollViewer, OnPinch);
        Gestures.AddPinchEndedHandler(m_scrollViewer, (_, _) => m_pinchStartZoom = null);
        m_scrollViewer.SizeChanged += (_, _) =>
        {
            if (m_fitToWindow)
                ZoomToFit();
        };

        m_zoomLabel = new TextBlock
        {
            Width = 54,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var fitButton = new Button { Content = "Fit", Padding = new Thickness(9, 5) };
        fitButton.Click += (_, _) => ZoomToFit();
        ToolTip.SetTip(fitButton, "Zoom to fit");
        var originalButton = new Button { Content = "100%", Padding = new Thickness(9, 5) };
        originalButton.Click += (_, _) => SetZoom(1, false);
        ToolTip.SetTip(originalButton, "Original size");
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(8),
            Background = Brush.Parse("#E624272D"),
            Children = { m_zoomLabel, fitButton, originalButton }
        };

        Content = new Grid { Children = { m_scrollViewer, controls } };
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(ZoomToFit, DispatcherPriority.Background);
    }

    internal static double GetFitZoom(PixelSize imageSize, Size viewport) =>
        imageSize.Width <= 0 || imageSize.Height <= 0 || viewport.Width <= 0 || viewport.Height <= 0
            ? 1
            : Math.Min(1, Math.Min(viewport.Width / imageSize.Width, viewport.Height / imageSize.Height));

    internal static double ApplyMagnification(double zoom, double magnification) =>
        Math.Clamp(zoom * Math.Max(0.1, 1 + magnification), MinimumZoom, MaximumZoom);

    internal static BitmapInterpolationMode GetInterpolationMode(double zoom) =>
        zoom > 1 ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;

    private void ZoomToFit() => SetZoom(GetFitZoom(m_bitmap.PixelSize, m_scrollViewer.Viewport), true);

    private void SetZoom(double zoom, bool fitToWindow, Point? anchor = null)
    {
        var previousZoom = m_zoom;
        m_zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        m_fitToWindow = fitToWindow;
        m_image.Width = m_bitmap.PixelSize.Width * m_zoom;
        m_image.Height = m_bitmap.PixelSize.Height * m_zoom;
        RenderOptions.SetBitmapInterpolationMode(m_image, GetInterpolationMode(m_zoom));
        m_zoomLabel.Text = $"{m_zoom:P0}";
        if (anchor == null || previousZoom <= 0)
        {
            m_scrollViewer.Offset = default;
            return;
        }

        var point = anchor.Value;
        var sourceX = (m_scrollViewer.Offset.X + point.X) / previousZoom;
        var sourceY = (m_scrollViewer.Offset.Y + point.Y) / previousZoom;
        Dispatcher.UIThread.Post(() =>
        {
            var maximumX = Math.Max(0, m_scrollViewer.Extent.Width - m_scrollViewer.Viewport.Width);
            var maximumY = Math.Max(0, m_scrollViewer.Extent.Height - m_scrollViewer.Viewport.Height);
            m_scrollViewer.Offset = new Vector(
                Math.Clamp(sourceX * m_zoom - point.X, 0, maximumX),
                Math.Clamp(sourceY * m_zoom - point.Y, 0, maximumY));
        }, DispatcherPriority.Background);
    }

    private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        var factor = Math.Pow(1.15, e.Delta.Y);
        SetZoom(m_zoom * factor, false, e.GetPosition(m_scrollViewer));
        e.Handled = true;
    }

    private void OnTouchPadMagnify(object sender, PointerDeltaEventArgs e)
    {
        SetZoom(ApplyMagnification(m_zoom, e.Delta.Y), false, e.GetPosition(m_scrollViewer));
        e.Handled = true;
    }

    private void OnPinch(object sender, PinchEventArgs e)
    {
        m_pinchStartZoom ??= m_zoom;
        SetZoom(m_pinchStartZoom.Value * e.Scale, false, e.ScaleOrigin);
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(m_image).Properties.IsLeftButtonPressed)
            return;
        m_panStart = e.GetPosition(m_scrollViewer);
        m_panStartOffset = m_scrollViewer.Offset;
        e.Pointer.Capture(m_image);
        m_image.Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (m_panStart == null)
            return;
        var position = e.GetPosition(m_scrollViewer);
        var delta = position - m_panStart.Value;
        m_scrollViewer.Offset = new Vector(m_panStartOffset.X - delta.X, m_panStartOffset.Y - delta.Y);
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (m_panStart == null)
            return;
        m_panStart = null;
        e.Pointer.Capture(null);
        m_image.Cursor = Cursor.Default;
        e.Handled = true;
    }

    public void Dispose()
    {
        m_image.Source = null;
        if (m_ownsBitmap)
            m_bitmap.Dispose();
    }
}
