namespace FolderAutomation.Features.FileConverter.Models;

public enum ImageProcessingMode
{
    MinMaxLimits,
    ExactTarget
}

public enum ImageSizeUnit
{
    KB,
    MB
}

public sealed class ImageProcessingSettings
{
    public ImageProcessingMode Mode { get; set; } =
        ImageProcessingMode.ExactTarget;

    // Minimum and maximum resolution
    public int? MinWidth { get; set; }

    public int? MinHeight { get; set; }

    public int? MaxWidth { get; set; }

    public int? MaxHeight { get; set; }

    // Minimum and maximum file size
    public double? MinFileSize { get; set; }

    public ImageSizeUnit MinFileSizeUnit { get; set; } =
        ImageSizeUnit.KB;

    public double? MaxFileSize { get; set; }

    public ImageSizeUnit MaxFileSizeUnit { get; set; } =
        ImageSizeUnit.KB;

    // Exact target resolution
    public int? TargetWidth { get; set; }

    public int? TargetHeight { get; set; }

    // Target file size
    public double? TargetFileSize { get; set; }

    public ImageSizeUnit TargetFileSizeUnit { get; set; } =
        ImageSizeUnit.KB;
}