// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Runtime.InteropServices;
using System.Xml;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Browse.Models;
using DTC.Core.Extensions;
using SkiaSharp;
using Svg.Skia;

namespace Browse.Services.Previews;

/// <summary>
/// Rasterizes SVG files into bounded bitmap previews.
/// </summary>
/// <remarks>
/// SVG parsing and rendering run away from the UI thread, with document and output limits to keep previews responsive.
/// </remarks>
public sealed class SvgPreviewProvider : IPreviewProvider
{
    private const int MaxPreviewDimension = 700;
    private const long MaxDocumentCharacters = 16 * 1024 * 1024;

    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            !item.IsDirectory && item.EffectiveExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase));

    public async Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
    {
        var decoded = await Task.Run(() => Decode((FileInfo)item.Info), cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            decoded.Bitmap.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        var details = $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g}\n" +
                      $"{decoded.Width:N0} × {decoded.Height:N0} · SVG vector image";
        return new ImagePreviewContent(item.Name, item.FullPath, details, decoded.Bitmap);
    }

    private static DecodedSvg Decode(FileInfo file)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            MaxCharactersInDocument = MaxDocumentCharacters,
            XmlResolver = null
        };
        using var stream = file.OpenRead();
        using var reader = XmlReader.Create(stream, settings);
        using var svg = new SKSvg();
        var picture = svg.Load(reader) ?? throw new InvalidDataException("The SVG contains no renderable content.");
        var bounds = picture.CullRect;
        if (!float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidDataException("The SVG has invalid dimensions.");

        var scale = Math.Min(1f, MaxPreviewDimension / Math.Max(bounds.Width, bounds.Height));
        var width = Math.Max(1, (int)MathF.Ceiling(bounds.Width * scale));
        var height = Math.Max(1, (int)MathF.Ceiling(bounds.Height * scale));
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul)) ??
                            throw new InvalidOperationException("The SVG preview surface could not be created.");
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.Scale(scale);
        surface.Canvas.Translate(-bounds.Left, -bounds.Top);
        surface.Canvas.DrawPicture(picture);
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        if (image.Width != width || image.Height != height)
            throw new InvalidOperationException($"The SVG preview surface returned {image.Width} × {image.Height} instead of {width} × {height}.");
        return new DecodedSvg(CreateBitmap(image), bounds.Width, bounds.Height);
    }

    private static Bitmap CreateBitmap(SKImage image)
    {
        using var pixels = image.PeekPixels() ??
                           throw new InvalidOperationException("The SVG preview pixels could not be read.");
        var bitmap = new WriteableBitmap(
            new PixelSize(image.Width, image.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        using var framebuffer = bitmap.Lock();
        var row = new byte[image.Width * 4];
        for (var y = 0; y < image.Height; y++)
        {
            Marshal.Copy(pixels.GetPixels() + y * pixels.RowBytes, row, 0, row.Length);
            Marshal.Copy(row, 0, framebuffer.Address + y * framebuffer.RowBytes, row.Length);
        }
        return bitmap;
    }

    private sealed record DecodedSvg(Bitmap Bitmap, float Width, float Height);
}
