using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace FolderAutomation.Features.FileConverter;

public sealed class ImageSizeOptimizer
{
    private readonly ImageEncoder _encoder = new();

    public long OptimizeJpeg(
        BitmapSource image,
        string outputPath,
        long targetSizeInBytes,
        int minimumQuality = 10,
        int maximumQuality = 100)
    {
        return OptimizeJpegWithinRange(
            image,
            outputPath,
            null,
            targetSizeInBytes,
            minimumQuality,
            maximumQuality);
    }

    public long OptimizeJpegWithinRange(
        BitmapSource image,
        string outputPath,
        long? minimumSizeInBytes,
        long? maximumSizeInBytes,
        int minimumQuality = 10,
        int maximumQuality = 100)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException(
                "An output file path is required.",
                nameof(outputPath));
        }

        if (!minimumSizeInBytes.HasValue &&
            !maximumSizeInBytes.HasValue)
        {
            throw new ArgumentException(
                "At least one file-size limit is required.");
        }

        if (minimumSizeInBytes.HasValue &&
            minimumSizeInBytes.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSizeInBytes));
        }

        if (maximumSizeInBytes.HasValue &&
            maximumSizeInBytes.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSizeInBytes));
        }

        if (minimumSizeInBytes.HasValue &&
            maximumSizeInBytes.HasValue &&
            minimumSizeInBytes.Value > maximumSizeInBytes.Value)
        {
            throw new ArgumentException(
                "Minimum file size cannot exceed maximum file size.");
        }

        if (minimumQuality < 1 ||
            maximumQuality > 100 ||
            minimumQuality > maximumQuality)
        {
            throw new ArgumentException(
                "JPEG quality must be between 1 and 100.");
        }

        string? directory = Path.GetDirectoryName(outputPath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "An output directory is required.",
                nameof(outputPath));
        }

        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(
            directory,
            $"{Guid.NewGuid():N}.jpg");

        try
        {
            int bestQuality = minimumQuality;
            long bestSizeDifference = long.MaxValue;
            bool foundWithinRange = false;

            // Test every quality level. This avoids relying on file size
            // being perfectly monotonic across JPEG encoder quality values.
            for (int quality = minimumQuality;
                 quality <= maximumQuality;
                 quality++)
            {
                _encoder.SaveJpeg(image, temporaryPath, quality);

                long currentSize = new FileInfo(temporaryPath).Length;

                bool meetsMinimum = !minimumSizeInBytes.HasValue ||
                    currentSize >= minimumSizeInBytes.Value;

                bool meetsMaximum = !maximumSizeInBytes.HasValue ||
                    currentSize <= maximumSizeInBytes.Value;

                if (meetsMinimum && meetsMaximum)
                {
                    // Prefer the highest quality that fits the range.
                    bestQuality = quality;
                    foundWithinRange = true;
                }
                else if (!foundWithinRange)
                {
                    long difference = 0;

                    if (minimumSizeInBytes.HasValue &&
                        currentSize < minimumSizeInBytes.Value)
                    {
                        difference = minimumSizeInBytes.Value - currentSize;
                    }
                    else if (maximumSizeInBytes.HasValue &&
                             currentSize > maximumSizeInBytes.Value)
                    {
                        difference = currentSize - maximumSizeInBytes.Value;
                    }

                    if (difference < bestSizeDifference)
                    {
                        bestSizeDifference = difference;
                        bestQuality = quality;
                    }
                }
            }

            _encoder.SaveJpeg(image, outputPath, bestQuality);

            return new FileInfo(outputPath).Length;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
