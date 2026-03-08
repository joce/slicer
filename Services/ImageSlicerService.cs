using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace slicer.Services;

/// <summary>
/// Slices images into strips and interlaces them (even indices first, then odd).
/// Uses Windows.Graphics.Imaging only.
/// </summary>
public static class ImageSlicerService
{
    private const int BytesPerPixel = 4; // Bgra8

    /// <summary>
    /// Ensures the bitmap is in Bgra8 format for consistent pixel access.
    /// </summary>
    public static SoftwareBitmap EnsureBgra8(SoftwareBitmap bitmap)
    {
        if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8)
            return bitmap;

        return SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    /// <summary>
    /// Slices the image horizontally into strips of the given height, then interlaces them.
    /// Order: 0, 2, 4, ... then 1, 3, 5, ...
    /// </summary>
    public static SoftwareBitmap SliceHorizontal(SoftwareBitmap source, int sliceHeight)
    {
        source = EnsureBgra8(source);
        int width = source.PixelWidth;
        int height = source.PixelHeight;

        if (sliceHeight >= height)
            return SoftwareBitmap.Copy(source);

        var strips = ExtractHorizontalStrips(source, width, height, sliceHeight);
        var interlaced = Interlace(strips);
        return ReassembleHorizontal(interlaced, width, height);
    }

    /// <summary>
    /// Slices the image vertically into strips of the given width, then interlaces them.
    /// Order: 0, 2, 4, ... then 1, 3, 5, ...
    /// </summary>
    public static SoftwareBitmap SliceVertical(SoftwareBitmap source, int sliceWidth)
    {
        source = EnsureBgra8(source);
        int width = source.PixelWidth;
        int height = source.PixelHeight;

        if (sliceWidth >= width)
            return SoftwareBitmap.Copy(source);

        var strips = ExtractVerticalStrips(source, width, height, sliceWidth);
        var interlaced = Interlace(strips);
        return ReassembleVertical(interlaced, width, height);
    }

    private static List<byte[]> ExtractHorizontalStrips(SoftwareBitmap source, int width, int height, int sliceHeight)
    {
        var pixels = new byte[width * height * BytesPerPixel];
        source.CopyToBuffer(pixels.AsBuffer());

        var strips = new List<byte[]>();
        for (int y = 0; y < height; y += sliceHeight)
        {
            int stripHeight = Math.Min(sliceHeight, height - y);
            int stripSize = width * stripHeight * BytesPerPixel;
            var strip = new byte[stripSize];

            for (int row = 0; row < stripHeight; row++)
            {
                int srcOffset = ((y + row) * width) * BytesPerPixel;
                int dstOffset = row * width * BytesPerPixel;
                Buffer.BlockCopy(pixels, srcOffset, strip, dstOffset, width * BytesPerPixel);
            }
            strips.Add(strip);
        }
        return strips;
    }

    private static List<byte[]> ExtractVerticalStrips(SoftwareBitmap source, int width, int height, int sliceWidth)
    {
        var pixels = new byte[width * height * BytesPerPixel];
        source.CopyToBuffer(pixels.AsBuffer());

        var strips = new List<byte[]>();
        for (int x = 0; x < width; x += sliceWidth)
        {
            int stripWidth = Math.Min(sliceWidth, width - x);
            int stripSize = stripWidth * height * BytesPerPixel;
            var strip = new byte[stripSize];

            for (int row = 0; row < height; row++)
            {
                int srcOffset = (row * width + x) * BytesPerPixel;
                int dstOffset = (row * stripWidth) * BytesPerPixel;
                Buffer.BlockCopy(pixels, srcOffset, strip, dstOffset, stripWidth * BytesPerPixel);
            }
            strips.Add(strip);
        }
        return strips;
    }

    private static List<T> Interlace<T>(List<T> items)
    {
        var evens = new List<T>();
        var odds = new List<T>();
        for (int i = 0; i < items.Count; i++)
        {
            if (i % 2 == 0)
                evens.Add(items[i]);
            else
                odds.Add(items[i]);
        }
        return evens.Concat(odds).ToList();
    }

    private static SoftwareBitmap ReassembleHorizontal(List<byte[]> strips, int width, int height)
    {
        var result = new byte[width * height * BytesPerPixel];
        int offset = 0;
        foreach (var strip in strips)
        {
            Buffer.BlockCopy(strip, 0, result, offset, strip.Length);
            offset += strip.Length;
        }

        return SoftwareBitmap.CreateCopyFromBuffer(
            result.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);
    }

    private static SoftwareBitmap ReassembleVertical(List<byte[]> strips, int width, int height)
    {
        var result = new byte[width * height * BytesPerPixel];
        int xOffset = 0;
        foreach (var strip in strips)
        {
            int sw = strip.Length / (height * BytesPerPixel);
            for (int row = 0; row < height; row++)
            {
                int srcOffset = row * sw * BytesPerPixel;
                int dstOffset = (row * width + xOffset) * BytesPerPixel;
                Buffer.BlockCopy(strip, srcOffset, result, dstOffset, sw * BytesPerPixel);
            }
            xOffset += sw;
        }

        return SoftwareBitmap.CreateCopyFromBuffer(
            result.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);
    }
}
