using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using FolderAutomation.Features.FileConverter.Models;

namespace FolderAutomation.Features.FileConverter;

public sealed class ImageProcessingService
{
    private static readonly string[] SupportedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff"
    ];

    public ImageFileInfo InspectImage(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A file path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("The selected image could not be found.", filePath);

        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (Array.IndexOf(SupportedExtensions, extension) < 0)
            throw new NotSupportedException("This image format is not supported.");

        using var stream = File.OpenRead(filePath);

        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
            throw new InvalidDataException("The selected file does not contain a readable image.");

        BitmapSource image = decoder.Frames[0];

        return new ImageFileInfo
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Width = image.PixelWidth,
            Height = image.PixelHeight,
            FileSizeInBytes = new FileInfo(filePath).Length,
            Format = extension.TrimStart('.').ToUpperInvariant()
        };
    }
}