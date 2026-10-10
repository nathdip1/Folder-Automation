using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderAutomation.Features.FileConverter;

public sealed class ImageResizer
{
    public BitmapSource Resize(
        BitmapSource source,
        int targetWidth,
        int targetHeight,
        bool preserveAspectRatio = true)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
            throw new ArgumentException(
                "The source image has invalid dimensions.",
                nameof(source));

        if (targetWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetWidth));

        if (targetHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetHeight));

        if (!preserveAspectRatio)
        {
            var stretched = new TransformedBitmap(
                source,
                new ScaleTransform(
                    (double)targetWidth / source.PixelWidth,
                    (double)targetHeight / source.PixelHeight));

            stretched.Freeze();
            return stretched;
        }

        double scale = Math.Max(
            (double)targetWidth / source.PixelWidth,
            (double)targetHeight / source.PixelHeight);

        int scaledWidth = Math.Max(
            targetWidth,
            (int)Math.Ceiling(source.PixelWidth * scale));

        int scaledHeight = Math.Max(
            targetHeight,
            (int)Math.Ceiling(source.PixelHeight * scale));

        var scaledImage = new TransformedBitmap(
            source,
            new ScaleTransform(
                (double)scaledWidth / source.PixelWidth,
                (double)scaledHeight / source.PixelHeight));

        scaledImage.Freeze();

        int cropX = Math.Max(0, (scaledWidth - targetWidth) / 2);
        int cropY = Math.Max(0, (scaledHeight - targetHeight) / 2);

        var cropRectangle = new Int32Rect(
            cropX,
            cropY,
            targetWidth,
            targetHeight);

        var croppedImage = new CroppedBitmap(
            scaledImage,
            cropRectangle);

        croppedImage.Freeze();

        return croppedImage;
    }
}