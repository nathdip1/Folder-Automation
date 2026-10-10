namespace FolderAutomation.Features.FileConverter.Models;

public sealed class ImageProcessingResult
{
    public required string TemporaryFilePath { get; init; }

    public required string FileName { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required long FileSizeInBytes { get; init; }

    public required string Format { get; init; }

    public required bool MeetsRequirements { get; init; }

    public required string StatusMessage { get; init; }

    public string Resolution => $"{Width} × {Height}";

    public string FileSize
    {
        get
        {
            if (FileSizeInBytes < 1024)
                return $"{FileSizeInBytes} B";

            if (FileSizeInBytes < 1024L * 1024)
                return $"{FileSizeInBytes / 1024.0:F1} KB";

            return $"{FileSizeInBytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}