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
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Browse.Services.Thumbnails;

/// <summary>
/// Retrieves cached or generated thumbnails through the Windows Shell image factory.
/// </summary>
/// <remarks>
/// HBITMAP pixels are copied into an Avalonia-owned bitmap before all native handles are released.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsShellThumbnailProvider : IPlatformThumbnailProvider
{
    public Task<Bitmap> CreateAsync(FileInfo file, int maximumDimension, CancellationToken cancellationToken) =>
        Task.Run(() => Create(file, maximumDimension, cancellationToken), cancellationToken);

    private static Bitmap Create(FileInfo file, int maximumDimension, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var comResult = CoInitializeEx(IntPtr.Zero, 0);
        IShellItemImageFactory factory = null;
        var bitmapHandle = IntPtr.Zero;
        try
        {
            var interfaceId = typeof(IShellItemImageFactory).GUID;
            var result = SHCreateItemFromParsingName(file.FullName, IntPtr.Zero, ref interfaceId, out factory);
            if (result < 0 || factory == null)
                return null;
            result = factory.GetImage(
                new NativeSize(maximumDimension, maximumDimension),
                ShellItemImageFactoryFlags.ThumbnailOnly | ShellItemImageFactoryFlags.BiggerSizeOk,
                out bitmapHandle);
            if (result < 0 || bitmapHandle == IntPtr.Zero)
                return null;
            cancellationToken.ThrowIfCancellationRequested();
            return CopyBitmap(bitmapHandle);
        }
        finally
        {
            if (bitmapHandle != IntPtr.Zero)
                DeleteObject(bitmapHandle);
            if (factory != null)
                Marshal.FinalReleaseComObject(factory);
            if (comResult >= 0)
                CoUninitialize();
        }
    }

    private static Bitmap CopyBitmap(IntPtr bitmapHandle)
    {
        if (GetObject(bitmapHandle, Marshal.SizeOf<NativeBitmap>(), out var bitmap) == 0)
            return null;
        var width = bitmap.Width;
        var height = Math.Abs(bitmap.Height);
        var pixels = new byte[checked(width * height * 4)];
        var info = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = 0
            }
        };
        var deviceContext = GetDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(deviceContext, bitmapHandle, 0, (uint)height, pixels, ref info, 0) == 0)
                return null;
        }

        finally
        {
            ReleaseDC(IntPtr.Zero, deviceContext);
        }

        if (!Enumerable.Range(0, width * height).Any(index => pixels[index * 4 + 3] != 0))
        {
            for (var index = 0; index < width * height; index++)
                pixels[index * 4 + 3] = 255;
        }

        var output = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        using var framebuffer = output.Lock();
        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(
                pixels,
                y * width * 4,
                framebuffer.Address + y * framebuffer.RowBytes,
                width * 4);
        }
        return output;
    }

    [Flags]
    private enum ShellItemImageFactoryFlags
    {
        BiggerSizeOk = 0x1,
        ThumbnailOnly = 0x8
    }

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, ShellItemImageFactoryFlags flags, out IntPtr bitmapHandle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize(int width, int height)
    {
        public readonly int Width = width;
        public readonly int Height = height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public IntPtr Bits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory shellItem);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr handle, int size, out NativeBitmap bitmap);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr deviceContext,
        IntPtr bitmap,
        uint startScan,
        uint scanLines,
        [Out] byte[] bits,
        ref BitmapInfo bitmapInfo,
        uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint concurrencyModel);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();
}
