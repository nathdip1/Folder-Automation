using System;
using System.IO;
using System.Windows.Media.Imaging;
using FolderAutomation.Features.FileConverter.Models;

namespace FolderAutomation.Features.FileConverter;

public sealed class ImageConversionService
{
    private readonly ImageProcessingService _inspectionService = new();
    private readonly ImageProcessingValidator _validator = new();
    private readonly ImageResizer _resizer = new();
    private readonly ImageEncoder _encoder = new();
    private readonly ImageSizeOptimizer _sizeOptimizer = new();

    public ImageFileInfo Inspect(string filePath)
    {
        return _inspectionService.InspectImage(filePath);
    }

    public ImageProcessingResult Process(
        string inputPath,
        ImageProcessingSettings settings,
        string outputFormat)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _validator.Validate(settings);

        ImageFileInfo sourceInfo = Inspect(inputPath);
        BitmapSource source = LoadImage(inputPath);
        string normalizedFormat = NormalizeFormat(outputFormat);
        string extension = normalizedFormat == "JPEG" ? ".jpg" : ".png";

        (int width, int height) = CalculateDimensions(source, settings);

        BitmapSource processed = _resizer.Resize(
            source,
            width,
            height,
            preserveAspectRatio: true);

        string outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "FolderAutomation",
            "ImageConverter");

        Directory.CreateDirectory(outputDirectory);

        string outputPath = Path.Combine(
            outputDirectory,
            $"{Guid.NewGuid():N}{extension}");

        try
        {
            long? minimumSizeBytes = null;
            long? maximumSizeBytes = null;

            if (settings.Mode == ImageProcessingMode.ExactTarget)
            {
                maximumSizeBytes = ToBytes(
                    settings.TargetFileSize!.Value,
                    settings.TargetFileSizeUnit);
            }
            else
            {
                if (settings.MinFileSize.HasValue)
                {
                    minimumSizeBytes = ToBytes(
                        settings.MinFileSize.Value,
                        settings.MinFileSizeUnit);
                }

                if (settings.MaxFileSize.HasValue)
                {
                    maximumSizeBytes = ToBytes(
                        settings.MaxFileSize.Value,
                        settings.MaxFileSizeUnit);
                }
            }

            if (normalizedFormat == "JPEG")
            {
                ProcessJpeg(
                    source,
                    processed,
                    outputPath,
                    settings,
                    minimumSizeBytes,
                    maximumSizeBytes);
            }
            else
            {
                ProcessPng(
                    source,
                    processed,
                    outputPath,
                    settings,
                    minimumSizeBytes,
                    maximumSizeBytes);
            }

            long outputSize = new FileInfo(outputPath).Length;

            BitmapSource finalImage = LoadImage(outputPath);

            bool meetsRequirements = MeetsRequirements(
                settings,
                finalImage.PixelWidth,
                finalImage.PixelHeight,
                outputSize);

            string statusMessage = meetsRequirements
                ? "The output meets all requested requirements."
                : "The output was created, but one or more requirements could not be met. Check the final resolution and file size.";

            return new ImageProcessingResult
            {
                TemporaryFilePath = outputPath,
                FileName = Path.GetFileNameWithoutExtension(sourceInfo.FileName) + extension,
                Width = finalImage.PixelWidth,
                Height = finalImage.PixelHeight,
                FileSizeInBytes = outputSize,
                Format = normalizedFormat,
                MeetsRequirements = meetsRequirements,
                StatusMessage = statusMessage
            };
        }
        catch
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            throw;
        }
    }

    private void ProcessJpeg(
        BitmapSource source,
        BitmapSource initialImage,
        string outputPath,
        ImageProcessingSettings settings,
        long? minimumSizeBytes,
        long? maximumSizeBytes)
    {
        if (settings.Mode == ImageProcessingMode.ExactTarget)
        {
            _sizeOptimizer.OptimizeJpeg(
                initialImage,
                outputPath,
                maximumSizeBytes!.Value);

            return;
        }

        // First try the largest resolution permitted by the limits.
        // The optimizer then searches JPEG quality settings.
        _sizeOptimizer.OptimizeJpegWithinRange(
            initialImage,
            outputPath,
            minimumSizeBytes,
            maximumSizeBytes);

        long outputSize = new FileInfo(outputPath).Length;

        bool meetsMinimum = !minimumSizeBytes.HasValue ||
            outputSize >= minimumSizeBytes.Value;

        bool meetsMaximum = !maximumSizeBytes.HasValue ||
            outputSize <= maximumSizeBytes.Value;

        if (meetsMinimum && meetsMaximum)
            return;

        // If the current file is too large, progressively try smaller
        // dimensions while respecting the configured resolution limits.
        if (maximumSizeBytes.HasValue && outputSize > maximumSizeBytes.Value)
        {
            foreach (BitmapSource candidate in GetSmallerCandidates(source, settings))
            {
                _sizeOptimizer.OptimizeJpegWithinRange(
                    candidate,
                    outputPath,
                    minimumSizeBytes,
                    maximumSizeBytes);

                outputSize = new FileInfo(outputPath).Length;

                if ((!minimumSizeBytes.HasValue ||
                     outputSize >= minimumSizeBytes.Value) &&
                    outputSize <= maximumSizeBytes.Value)
                {
                    return;
                }
            }
        }
    }

    private void ProcessPng(
        BitmapSource source,
        BitmapSource initialImage,
        string outputPath,
        ImageProcessingSettings settings,
        long? minimumSizeBytes,
        long? maximumSizeBytes)
    {
        _encoder.SavePng(initialImage, outputPath);

        long outputSize = new FileInfo(outputPath).Length;

        if (settings.Mode == ImageProcessingMode.ExactTarget ||
            !maximumSizeBytes.HasValue ||
            outputSize <= maximumSizeBytes.Value)
        {
            return;
        }

        foreach (BitmapSource candidate in GetSmallerCandidates(source, settings))
        {
            _encoder.SavePng(candidate, outputPath);

            outputSize = new FileInfo(outputPath).Length;

            if (outputSize <= maximumSizeBytes.Value &&
                (!minimumSizeBytes.HasValue ||
                 outputSize >= minimumSizeBytes.Value))
            {
                return;
            }
        }
    }

    private System.Collections.Generic.IEnumerable<BitmapSource> GetSmallerCandidates(
        BitmapSource source,
        ImageProcessingSettings settings)
    {
        int minimumWidth = settings.MinWidth ?? 1;
        int minimumHeight = settings.MinHeight ?? 1;
        int maximumWidth = settings.MaxWidth ?? int.MaxValue;
        int maximumHeight = settings.MaxHeight ?? int.MaxValue;

        int originalWidth = source.PixelWidth;
        int originalHeight = source.PixelHeight;

        for (double scale = 0.95; scale >= 0.10; scale -= 0.05)
        {
            int candidateWidth = Math.Max(
                1,
                (int)Math.Floor(originalWidth * scale));

            int candidateHeight = Math.Max(
                1,
                (int)Math.Floor(originalHeight * scale));

            if (candidateWidth < minimumWidth ||
                candidateHeight < minimumHeight ||
                candidateWidth > maximumWidth ||
                candidateHeight > maximumHeight)
            {
                continue;
            }

            yield return _resizer.Resize(
                source,
                candidateWidth,
                candidateHeight,
                preserveAspectRatio: true);
        }
    }

    private static BitmapSource LoadImage(string inputPath)
    {
        using var stream = File.OpenRead(inputPath);

        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
            throw new InvalidDataException(
                "The image contains no readable frames.");

        BitmapSource source = decoder.Frames[0];
        source.Freeze();

        return source;
    }

    private static (int Width, int Height) CalculateDimensions(
        BitmapSource source,
        ImageProcessingSettings settings)
    {
        if (settings.Mode == ImageProcessingMode.ExactTarget)
        {
            return (
                settings.TargetWidth!.Value,
                settings.TargetHeight!.Value);
        }

        int width = source.PixelWidth;
        int height = source.PixelHeight;

        int minimumWidth = settings.MinWidth ?? 1;
        int minimumHeight = settings.MinHeight ?? 1;
        int maximumWidth = settings.MaxWidth ?? int.MaxValue;
        int maximumHeight = settings.MaxHeight ?? int.MaxValue;

        if (minimumWidth > maximumWidth ||
            minimumHeight > maximumHeight)
        {
            throw new ArgumentException(
                "The resolution constraints are contradictory.");
        }

        double minimumScale = Math.Max(
            (double)minimumWidth / width,
            (double)minimumHeight / height);

        double maximumScale = Math.Min(
            (double)maximumWidth / width,
            (double)maximumHeight / height);

        if (minimumScale > maximumScale)
        {
            throw new InvalidOperationException(
                "The minimum and maximum resolution requirements cannot both be satisfied while preserving the original aspect ratio.");
        }

        double scale;

        if (minimumScale > 1)
            scale = minimumScale;
        else if (maximumScale < 1)
            scale = maximumScale;
        else
            scale = 1;

        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static string NormalizeFormat(string outputFormat)
    {
        if (string.IsNullOrWhiteSpace(outputFormat))
            throw new ArgumentException(
                "Select an output format.",
                nameof(outputFormat));

        return outputFormat.Trim().ToUpperInvariant() switch
        {
            "JPG" or "JPEG" => "JPEG",
            "PNG" => "PNG",
            _ => throw new NotSupportedException(
                "Supported output formats are JPEG and PNG.")
        };
    }

    private static bool MeetsRequirements(
        ImageProcessingSettings settings,
        int width,
        int height,
        long fileSize)
    {
        if (settings.Mode == ImageProcessingMode.ExactTarget)
        {
            long targetSize = ToBytes(
                settings.TargetFileSize!.Value,
                settings.TargetFileSizeUnit);

            return width == settings.TargetWidth!.Value
                && height == settings.TargetHeight!.Value
                && fileSize <= targetSize;
        }

        if (settings.MinWidth.HasValue &&
            width < settings.MinWidth.Value)
            return false;

        if (settings.MinHeight.HasValue &&
            height < settings.MinHeight.Value)
            return false;

        if (settings.MaxWidth.HasValue &&
            width > settings.MaxWidth.Value)
            return false;

        if (settings.MaxHeight.HasValue &&
            height > settings.MaxHeight.Value)
            return false;

        if (settings.MinFileSize.HasValue &&
            fileSize < ToBytes(
                settings.MinFileSize.Value,
                settings.MinFileSizeUnit))
            return false;

        if (settings.MaxFileSize.HasValue &&
            fileSize > ToBytes(
                settings.MaxFileSize.Value,
                settings.MaxFileSizeUnit))
            return false;

        return true;
    }

    private static long ToBytes(double value, ImageSizeUnit unit)
    {
        double multiplier = unit == ImageSizeUnit.MB
            ? 1024.0 * 1024.0
            : 1024.0;

        return checked((long)Math.Round(value * multiplier));
    }
}
