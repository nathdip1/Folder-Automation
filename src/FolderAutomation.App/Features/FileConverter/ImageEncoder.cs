using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace FolderAutomation.Features.FileConverter;

public sealed class ImageEncoder
{
    public void SaveJpeg(BitmapSource image, string outputPath, int quality)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("An output path is required.", nameof(outputPath));

        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(
                nameof(quality), "JPEG quality must be between 1 and 100.");

        string? directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var encoder = new JpegBitmapEncoder
        {
            QualityLevel = quality
        };

        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        encoder.Save(stream);
    }

    public void SavePng(BitmapSource image, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("An output path is required.", nameof(outputPath));

        string? directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        encoder.Save(stream);
    }
}