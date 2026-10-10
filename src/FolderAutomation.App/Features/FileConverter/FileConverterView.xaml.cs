using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using FolderAutomation.Features.FileConverter.Models;

namespace FolderAutomation.Features.FileConverter;

public partial class FileConverterView : UserControl
{
    private readonly ImageConversionService _conversionService = new();
    private readonly FileConverterViewModel _viewModel = new();

    private ImageProcessingResult? _currentResult;
    private string? _selectedFilePath;

    public FileConverterView()
    {
        InitializeComponent();
        DataContext = _viewModel;
        SaveOutputButton.IsEnabled = false;
    }

    private void ChooseImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a Photo",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        try
        {
            ImageFileInfo imageInfo = _conversionService.Inspect(dialog.FileName);
            BitmapSource preview = LoadPreview(dialog.FileName);

            _selectedFilePath = dialog.FileName;
            _viewModel.SelectedImage = imageInfo;
            _viewModel.ProcessingResult = null;
            _currentResult = null;

            SelectedImagePreview.Source = preview;
            SelectedImagePreview.Visibility = Visibility.Visible;

            SelectedImageNameText.Text = imageInfo.FileName;
            SelectedImageNameText.Visibility = Visibility.Visible;

            SelectedImageDetailsText.Text =
                $"Resolution: {imageInfo.Resolution} px\n" +
                $"File size: {imageInfo.FileSize}\n" +
                $"Format: {imageInfo.Format}";

            SelectedImageDetailsText.Visibility = Visibility.Visible;

            ProcessedImagePreview.Source = null;
            ProcessedImageDetailsText.Text = string.Empty;
            ValidationResultText.Text = string.Empty;
            ResultPanel.Visibility = Visibility.Collapsed;
            SaveOutputButton.IsEnabled = false;

            _viewModel.StatusMessage =
                "Photo selected. Enter your output requirements.";

            StatusMessageText.Text = _viewModel.StatusMessage;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to open the selected image.\n\n{ex.Message}",
                "Image Selection Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ProcessingModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (MinMaxSettingsPanel == null || ExactTargetSettingsPanel == null)
            return;

        bool exactTarget = ExactTargetModeRadio.IsChecked == true;

        MinMaxSettingsPanel.Visibility = exactTarget
            ? Visibility.Collapsed
            : Visibility.Visible;

        ExactTargetSettingsPanel.Visibility = exactTarget
            ? Visibility.Visible
            : Visibility.Collapsed;

        _viewModel.SelectedMode = exactTarget
            ? ImageProcessingMode.ExactTarget
            : ImageProcessingMode.MinMaxLimits;
    }

    private void ProcessImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            MessageBox.Show(
                "Please select a photo first.",
                "No Photo Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        // Every click starts a fresh attempt using the current settings.
        _currentResult = null;
        _viewModel.ProcessingResult = null;

        ProcessedImagePreview.Source = null;
        ProcessedImageDetailsText.Text = string.Empty;
        ValidationResultText.Text = string.Empty;
        ResultPanel.Visibility = Visibility.Collapsed;
        SaveOutputButton.IsEnabled = false;

        try
        {
            ImageProcessingSettings settings = BuildSettings();

            string outputFormat =
                (OutputFormatComboBox.SelectedItem as ComboBoxItem)?
                    .Content?.ToString() ?? "JPEG";

            _viewModel.IsProcessing = true;
            ProcessImageButton.IsEnabled = false;
            StatusMessageText.Text = "Processing photo...";

            ImageProcessingResult result = _conversionService.Process(
                _selectedFilePath,
                settings,
                outputFormat);

            BitmapSource resultPreview = LoadPreview(result.TemporaryFilePath);

            _currentResult = result;
            _viewModel.ProcessingResult = result;

            ProcessedImagePreview.Source = resultPreview;

            ProcessedImageDetailsText.Text =
                $"Resolution: {result.Resolution} px\n" +
                $"File size: {result.FileSize}\n" +
                $"Format: {result.Format}";

            ValidationResultText.Text = result.StatusMessage;
            ValidationResultText.Foreground = result.MeetsRequirements
                ? Brushes.ForestGreen
                : Brushes.DarkOrange;

            ResultPanel.Visibility = Visibility.Visible;
            SaveOutputButton.IsEnabled = true;

            StatusMessageText.Text = result.MeetsRequirements
                ? "Processing finished. All requirements were met."
                : "Processing finished, but some requirements were not met.";
        }
        catch (Exception ex)
        {
            _currentResult = null;
            _viewModel.ProcessingResult = null;

            ProcessedImagePreview.Source = null;
            ResultPanel.Visibility = Visibility.Collapsed;
            SaveOutputButton.IsEnabled = false;

            StatusMessageText.Text = "Processing failed.";

            MessageBox.Show(
                $"Unable to process the photo.\n\n{ex.Message}",
                "Processing Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsProcessing = false;
            ProcessImageButton.IsEnabled = true;
        }
    }

    private void SaveOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult == null ||
            !File.Exists(_currentResult.TemporaryFilePath))
        {
            MessageBox.Show(
                "Process a photo before saving the output.",
                "No Processed Photo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save Processed Photo",
            FileName = _currentResult.FileName,
            Filter = _currentResult.Format == "PNG"
                ? "PNG image|*.png"
                : "JPEG image|*.jpg",
            DefaultExt = _currentResult.Format == "PNG" ? ".png" : ".jpg",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        try
        {
            File.Copy(
                _currentResult.TemporaryFilePath,
                dialog.FileName,
                overwrite: true);

            MessageBox.Show(
                $"Photo saved successfully.\n\n{dialog.FileName}",
                "Photo Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to save the photo.\n\n{ex.Message}",
                "Save Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private ImageProcessingSettings BuildSettings()
    {
        if (ExactTargetModeRadio.IsChecked == true)
        {
            return new ImageProcessingSettings
            {
                Mode = ImageProcessingMode.ExactTarget,
                TargetWidth = ParseRequiredInteger(
                    TargetWidthTextBox.Text, "Target width"),
                TargetHeight = ParseRequiredInteger(
                    TargetHeightTextBox.Text, "Target height"),
                TargetFileSize = ParseRequiredDouble(
                    TargetFileSizeTextBox.Text, "Target file size"),
                TargetFileSizeUnit = ParseSizeUnit(
                    TargetFileSizeUnitComboBox)
            };
        }

        return new ImageProcessingSettings
        {
            Mode = ImageProcessingMode.MinMaxLimits,
            MinWidth = ParseOptionalInteger(
                MinWidthTextBox.Text, "Minimum width"),
            MinHeight = ParseOptionalInteger(
                MinHeightTextBox.Text, "Minimum height"),
            MaxWidth = ParseOptionalInteger(
                MaxWidthTextBox.Text, "Maximum width"),
            MaxHeight = ParseOptionalInteger(
                MaxHeightTextBox.Text, "Maximum height"),
            MinFileSize = ParseOptionalDouble(
                MinFileSizeTextBox.Text, "Minimum file size"),
            MinFileSizeUnit = ParseSizeUnit(
                MinFileSizeUnitComboBox),
            MaxFileSize = ParseOptionalDouble(
                MaxFileSizeTextBox.Text, "Maximum file size"),
            MaxFileSizeUnit = ParseSizeUnit(
                MaxFileSizeUnitComboBox)
        };
    }

    private static int ParseRequiredInteger(string value, string fieldName)
    {
        if (!int.TryParse(value, out int result) || result <= 0)
        {
            throw new ArgumentException(
                $"{fieldName} must be a positive whole number.");
        }

        return result;
    }

    private static int? ParseOptionalInteger(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return ParseRequiredInteger(value, fieldName);
    }

    private static double ParseRequiredDouble(string value, string fieldName)
    {
        if (!double.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out double result) ||
            result <= 0 ||
            double.IsInfinity(result) ||
            double.IsNaN(result))
        {
            throw new ArgumentException(
                $"{fieldName} must be a positive number. Use a period for decimals.");
        }

        return result;
    }

    private static double? ParseOptionalDouble(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return ParseRequiredDouble(value, fieldName);
    }

    private static ImageSizeUnit ParseSizeUnit(ComboBox comboBox)
    {
        string unit =
            (comboBox.SelectedItem as ComboBoxItem)?
                .Content?.ToString() ?? "KB";

        return unit == "MB"
            ? ImageSizeUnit.MB
            : ImageSizeUnit.KB;
    }

    private static BitmapSource LoadPreview(string filePath)
    {
        using var stream = File.OpenRead(filePath);

        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
        {
            throw new InvalidDataException(
                "The image contains no readable frames.");
        }

        BitmapSource source = decoder.Frames[0];
        source.Freeze();

        return source;
    }
}
