// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using BitMiracle.LibTiff.Classic;
using Browse.Models;
using DTC.Core.Extensions;

namespace Browse.Services.Previews;

/// <summary>
/// Decodes common images into bounded bitmap previews and reports source metadata.
/// </summary>
/// <remarks>
/// TIFF scanlines are sampled while decoding so very large source images do not require a full raster allocation.
/// </remarks>
public sealed class ImagePreviewProvider : IPreviewProvider
{
    private const int MaxPreviewDimension = 700;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
    };

    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(!item.IsDirectory && Extensions.Contains(Path.GetExtension(item.Name)));

    public async Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken)
    {
        var decoded = await Task.Run(() => Decode((FileInfo)item.Info), cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            decoded.Bitmap.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        var details = $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g}\n" +
                      $"{decoded.Width:N0} × {decoded.Height:N0} · {decoded.BitDepth}";
        return new ImagePreviewContent(item.Name, item.FullPath, details, decoded.Bitmap);
    }

    private static DecodedImage Decode(FileInfo file)
    {
        if (!file.Extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) &&
            !file.Extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = file.OpenRead();
            var decodedBitmap = Bitmap.DecodeToHeight(stream, MaxPreviewDimension, BitmapInterpolationMode.MediumQuality);
            var metadata = ReadMetadata(file);
            return new DecodedImage(
                decodedBitmap,
                metadata?.Width ?? decodedBitmap.PixelSize.Width,
                metadata?.Height ?? decodedBitmap.PixelSize.Height,
                metadata?.BitDepth ?? "32 bpp preview");
        }

        using var tiff = Tiff.Open(file.FullName, "r") ?? throw new IOException("The TIFF file could not be opened.");
        var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        var bitsPerSample = tiff.GetFieldDefaulted(TiffTag.BITSPERSAMPLE)[0].ToInt();
        var samplesPerPixel = tiff.GetFieldDefaulted(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
        var photometric = (Photometric)tiff.GetFieldDefaulted(TiffTag.PHOTOMETRIC)[0].ToInt();
        var planarConfig = (PlanarConfig)tiff.GetFieldDefaulted(TiffTag.PLANARCONFIG)[0].ToInt();
        var scale = Math.Min(1.0, (double)MaxPreviewDimension / Math.Max(width, height));
        var previewWidth = Math.Max(1, (int)Math.Round(width * scale));
        var previewHeight = Math.Max(1, (int)Math.Round(height * scale));
        var pixels = new byte[previewWidth * previewHeight * 4];
        if (bitsPerSample == 8 && planarConfig == PlanarConfig.CONTIG &&
            (photometric == Photometric.RGB || photometric is Photometric.MINISBLACK or Photometric.MINISWHITE))
        {
            DecodeTiffScanlines(tiff, pixels, width, height, previewWidth, previewHeight, scale, samplesPerPixel, photometric);
        }
        else
        {
            DecodeTiffRaster(tiff, pixels, width, height, previewWidth, previewHeight, scale);
        }

        var bitmap = CreateBitmap(pixels, previewWidth, previewHeight);
        return new DecodedImage(bitmap, width, height, $"{samplesPerPixel} × {bitsPerSample} bpp");
    }

    private static void DecodeTiffScanlines(
        Tiff tiff,
        byte[] pixels,
        int width,
        int height,
        int previewWidth,
        int previewHeight,
        double scale,
        int samplesPerPixel,
        Photometric photometric)
    {
        var scanline = new byte[tiff.ScanlineSize()];
        var targetY = 0;
        for (var sourceY = 0; sourceY < height && targetY < previewHeight; sourceY++)
        {
            if (!tiff.ReadScanline(scanline, sourceY))
                throw new IOException("The TIFF image could not be decoded.");
            if (sourceY != Math.Min(height - 1, (int)(targetY / scale)))
                continue;
            CopyTiffScanline(scanline, pixels, targetY, previewWidth, width, scale, samplesPerPixel, photometric);
            targetY++;
        }
    }

    private static void DecodeTiffRaster(
        Tiff tiff,
        byte[] pixels,
        int width,
        int height,
        int previewWidth,
        int previewHeight,
        double scale)
    {
        const int maxFallbackPixels = 16 * 1024 * 1024;
        if ((long)width * height > maxFallbackPixels)
            throw new IOException("This TIFF format is too large to preview safely.");
        var raster = new int[checked(width * height)];
        if (!tiff.ReadRGBAImageOriented(width, height, raster, Orientation.TOPLEFT))
            throw new IOException("The TIFF image could not be decoded.");
        for (var y = 0; y < previewHeight; y++)
        {
            var sourceY = Math.Min(height - 1, (int)(y / scale));
            for (var x = 0; x < previewWidth; x++)
            {
                var sourceX = Math.Min(width - 1, (int)(x / scale));
                var rgba = raster[sourceY * width + sourceX];
                var offset = (y * previewWidth + x) * 4;
                pixels[offset] = (byte)Tiff.GetB(rgba);
                pixels[offset + 1] = (byte)Tiff.GetG(rgba);
                pixels[offset + 2] = (byte)Tiff.GetR(rgba);
                pixels[offset + 3] = (byte)Tiff.GetA(rgba);
            }
        }
    }

    private static void CopyTiffScanline(
        byte[] scanline,
        byte[] pixels,
        int targetY,
        int previewWidth,
        int sourceWidth,
        double scale,
        int samplesPerPixel,
        Photometric photometric)
    {
        for (var x = 0; x < previewWidth; x++)
        {
            var sourceX = Math.Min(sourceWidth - 1, (int)(x / scale));
            var sourceOffset = sourceX * samplesPerPixel;
            var targetOffset = (targetY * previewWidth + x) * 4;
            if (photometric == Photometric.RGB)
            {
                pixels[targetOffset] = scanline[sourceOffset + 2];
                pixels[targetOffset + 1] = scanline[sourceOffset + 1];
                pixels[targetOffset + 2] = scanline[sourceOffset];
                pixels[targetOffset + 3] = samplesPerPixel > 3 ? scanline[sourceOffset + 3] : (byte)255;
            }
            else
            {
                var intensity = scanline[sourceOffset];
                if (photometric == Photometric.MINISWHITE)
                    intensity = (byte)(255 - intensity);
                pixels[targetOffset] = intensity;
                pixels[targetOffset + 1] = intensity;
                pixels[targetOffset + 2] = intensity;
                pixels[targetOffset + 3] = samplesPerPixel > 1 ? scanline[sourceOffset + 1] : (byte)255;
            }
        }
    }

    private static Bitmap CreateBitmap(byte[] pixels, int width, int height)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        using var framebuffer = bitmap.Lock();
        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(
                pixels,
                y * width * 4,
                framebuffer.Address + y * framebuffer.RowBytes,
                width * 4);
        }
        return bitmap;
    }

    private static ImageMetadata ReadMetadata(FileInfo file)
    {
        try
        {
            using var stream = file.OpenRead();
            var header = new byte[32];
            if (stream.Read(header, 0, header.Length) < 30)
                return null;
            if (file.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                var width = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
                var height = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
                var channels = header[25] switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 1 };
                return new ImageMetadata(width, height, $"{channels} × {header[24]} bpp");
            }
            if (file.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                return new ImageMetadata(
                    BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6, 2)),
                    BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8, 2)),
                    $"{(header[10] & 0x07) + 1} bpp indexed");
            }
            if (file.Extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                var bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28, 2));
                return new ImageMetadata(
                    Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(18, 4))),
                    Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(22, 4))),
                    bitsPerPixel is 24 or 32 ? $"{bitsPerPixel / 8} × 8 bpp" : $"{bitsPerPixel} bpp");
            }
            if (file.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                return ReadJpegMetadata(stream);
        }
        catch (Exception)
        {
            // Metadata is optional; image decoding can still proceed.
        }
        return null;
    }

    private static ImageMetadata ReadJpegMetadata(Stream stream)
    {
        stream.Position = 2;
        while (stream.Position < stream.Length - 8)
        {
            if (stream.ReadByte() != 0xff)
                continue;
            int marker;
            do marker = stream.ReadByte(); while (marker == 0xff);
            if (marker < 0)
                break;
            var lengthBytes = new byte[2];
            if (stream.Read(lengthBytes, 0, 2) != 2)
                break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
            if (length < 2)
                break;
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
            {
                var frame = new byte[6];
                if (stream.Read(frame, 0, frame.Length) != frame.Length)
                    break;
                return new ImageMetadata(
                    BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(3, 2)),
                    BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(1, 2)),
                    $"{frame[5]} × {frame[0]} bpp");
            }
            stream.Seek(length - 2, SeekOrigin.Current);
        }
        return null;
    }

    private sealed record DecodedImage(Bitmap Bitmap, int Width, int Height, string BitDepth);
    private sealed record ImageMetadata(int Width, int Height, string BitDepth);
}
