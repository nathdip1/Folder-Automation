using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FolderAutomation.Features.FolderOrganizer.Models;
using FolderAutomation.Features.OrganizationPreview;

namespace FolderAutomation.Features.FolderOrganizer;

/// <summary>
/// Holds the presentation state for the Folder Organizer view.
/// WPF-specific dialogs and event handling remain in FolderOrganizerView.
/// </summary>
public sealed class FolderOrganizerViewModel : INotifyPropertyChanged
{
    private string _selectedFolderPath = string.Empty;
    private string _resultsTitle = "Files";
    private string _resultsSubtitle = "Files found in the selected folder";
    private string _fileCountText = "0 files";
    private string _previewSummary = string.Empty;
    private string _selectedConflictAction = "Ask me each time";
    private bool _isPreviewVisible;
    private bool _isConflictActionEnabled = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FileDisplayItem> Files { get; } = new();

    public ObservableCollection<OrganizationPreviewRow> PreviewRows { get; } = new();

    public string SelectedFolderPath
    {
        get => _selectedFolderPath;
        set => SetProperty(ref _selectedFolderPath, value);
    }

    public string ResultsTitle
    {
        get => _resultsTitle;
        set => SetProperty(ref _resultsTitle, value);
    }

    public string ResultsSubtitle
    {
        get => _resultsSubtitle;
        set => SetProperty(ref _resultsSubtitle, value);
    }

    public string FileCountText
    {
        get => _fileCountText;
        set => SetProperty(ref _fileCountText, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        set => SetProperty(ref _previewSummary, value);
    }

    public string SelectedConflictAction
    {
        get => _selectedConflictAction;
        set => SetProperty(ref _selectedConflictAction, value);
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set
        {
            if (SetProperty(ref _isPreviewVisible, value))
            {
                OnPropertyChanged(nameof(IsFileListVisible));
            }
        }
    }

    public bool IsFileListVisible => !IsPreviewVisible;

    public bool IsConflictActionEnabled
    {
        get => _isConflictActionEnabled;
        set => SetProperty(ref _isConflictActionEnabled, value);
    }

    public void ClearPreview()
    {
        PreviewRows.Clear();
        PreviewSummary = string.Empty;
        IsPreviewVisible = false;
        IsConflictActionEnabled = true;
        ResultsTitle = "Files";
        ResultsSubtitle = "Files found in the selected folder";
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
