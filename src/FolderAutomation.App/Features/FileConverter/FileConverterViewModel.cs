using System.ComponentModel;
using System.Runtime.CompilerServices;

using FolderAutomation.Features.FileConverter.Models;

namespace FolderAutomation.Features.FileConverter;

public sealed class FileConverterViewModel : INotifyPropertyChanged
{
    private ImageFileInfo? _selectedImage;
    private ImageProcessingResult? _processingResult;
    private ImageProcessingMode _selectedMode = ImageProcessingMode.ExactTarget;
    private string _statusMessage = "Select an image to get started.";
    private bool _isProcessing;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageFileInfo? SelectedImage
    {
        get => _selectedImage;
        set => SetProperty(ref _selectedImage, value);
    }

    public ImageProcessingResult? ProcessingResult
    {
        get => _processingResult;
        set => SetProperty(ref _processingResult, value);
    }

    public ImageProcessingMode SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    private void SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}