using System;
using FolderAutomation.Features.FileConverter.Models;

namespace FolderAutomation.Features.FileConverter;

public sealed class ImageProcessingValidator
{
    public void Validate(ImageProcessingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Enum.IsDefined(settings.Mode))
        {
            throw new ArgumentException(
                "Select a supported image processing mode.");
        }

        if (!Enum.IsDefined(settings.MinFileSizeUnit) ||
            !Enum.IsDefined(settings.MaxFileSizeUnit) ||
            !Enum.IsDefined(settings.TargetFileSizeUnit))
        {
            throw new ArgumentException(
                "Select a supported file-size unit.");
        }

        if (settings.Mode == ImageProcessingMode.ExactTarget)
        {
            ValidateDimension(settings.TargetWidth, "Target width");
            ValidateDimension(settings.TargetHeight, "Target height");
            ValidateFileSize(settings.TargetFileSize, "Target file size");
            return;
        }

        ValidateOptionalDimension(settings.MinWidth, "Minimum width");
        ValidateOptionalDimension(settings.MinHeight, "Minimum height");
        ValidateOptionalDimension(settings.MaxWidth, "Maximum width");
        ValidateOptionalDimension(settings.MaxHeight, "Maximum height");

        ValidateOptionalFileSize(settings.MinFileSize, "Minimum file size");
        ValidateOptionalFileSize(settings.MaxFileSize, "Maximum file size");

        if (settings.MinWidth.HasValue &&
            settings.MaxWidth.HasValue &&
            settings.MinWidth.Value > settings.MaxWidth.Value)
        {
            throw new ArgumentException(
                "Minimum width cannot exceed maximum width.");
        }

        if (settings.MinHeight.HasValue &&
            settings.MaxHeight.HasValue &&
            settings.MinHeight.Value > settings.MaxHeight.Value)
        {
            throw new ArgumentException(
                "Minimum height cannot exceed maximum height.");
        }

        if (settings.MinFileSize.HasValue &&
            settings.MaxFileSize.HasValue &&
            ToBytes(settings.MinFileSize.Value, settings.MinFileSizeUnit) >
            ToBytes(settings.MaxFileSize.Value, settings.MaxFileSizeUnit))
        {
            throw new ArgumentException(
                "Minimum file size cannot exceed maximum file size.");
        }

        if (!settings.MinWidth.HasValue &&
            !settings.MinHeight.HasValue &&
            !settings.MaxWidth.HasValue &&
            !settings.MaxHeight.HasValue &&
            !settings.MinFileSize.HasValue &&
            !settings.MaxFileSize.HasValue)
        {
            throw new ArgumentException(
                "Enter at least one minimum or maximum requirement.");
        }
    }

    private static void ValidateDimension(int? value, string name)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            throw new ArgumentException(
                $"{name} must be greater than zero.");
        }
    }

    private static void ValidateOptionalDimension(int? value, string name)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new ArgumentException(
                $"{name} must be greater than zero.");
        }
    }

    private static void ValidateFileSize(double? value, string name)
    {
        if (!value.HasValue ||
            value.Value <= 0 ||
            double.IsNaN(value.Value) ||
            double.IsInfinity(value.Value))
        {
            throw new ArgumentException(
                $"{name} must be a valid number greater than zero.");
        }
    }

    private static void ValidateOptionalFileSize(double? value, string name)
    {
        if (value.HasValue)
        {
            ValidateFileSize(value, name);
        }
    }

    private static long ToBytes(double value, ImageSizeUnit unit)
    {
        double multiplier = unit switch
        {
            ImageSizeUnit.KB => 1024.0,
            ImageSizeUnit.MB => 1024.0 * 1024.0,
            _ => throw new ArgumentException(
                "Unsupported file-size unit.")
        };

        double bytes = value * multiplier;

        if (double.IsNaN(bytes) ||
            double.IsInfinity(bytes) ||
            bytes > long.MaxValue)
        {
            throw new ArgumentException(
                "The specified file size is too large.");
        }

        return checked((long)Math.Round(bytes));
    }
}