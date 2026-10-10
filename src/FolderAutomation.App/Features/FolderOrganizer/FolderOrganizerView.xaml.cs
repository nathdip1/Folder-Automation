using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FolderAutomation.Core;
using FolderAutomation.Data;
using FolderAutomation.Features.FolderOrganizer.Models;
using FolderAutomation.Features.OrganizationPreview;

namespace FolderAutomation.Features.FolderOrganizer;

public partial class FolderOrganizerView : UserControl
{
    private readonly FileService _fileService;
    private readonly OrganizationPlanner _organizationPlanner;
    private readonly OrganizationService _organizationService;
    private readonly UndoRepository _undoRepository;
    private readonly UndoService _undoService;
    private readonly FolderOrganizerViewModel _viewModel;

    private IReadOnlyList<OrganizationItem>? _pendingOrganizationPlan;
    private string? _pendingOrganizationFolderPath;
    private FileConflictAction _pendingConflictAction = FileConflictAction.Ask;
    private bool _applyConflictActionToRemaining;
    private FileConflictAction _rememberedConflictAction = FileConflictAction.Skip;

    public FolderOrganizerView()
    {
        InitializeComponent();

        _viewModel = new FolderOrganizerViewModel();
        DataContext = _viewModel;

        _fileService = new FileService();
        _organizationPlanner = new OrganizationPlanner();

        string applicationDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderAutomation");
        string databasePath = Path.Combine(applicationDataFolder, "FolderAutomation.db");

        var databaseConnection = new DatabaseConnection(databasePath);
        _undoRepository = new UndoRepository(databaseConnection);
        _undoRepository.CreateTable();
        _organizationService = new OrganizationService(_undoRepository);
        _undoService = new UndoService();
    }

    public string SelectedFolderPath => _viewModel.SelectedFolderPath;

    public void SetSelectedFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        _viewModel.SelectedFolderPath = folderPath;
        LoadFiles(folderPath);
    }

    public void SetConflictAction(string actionText)
    {
        _viewModel.SelectedConflictAction = string.IsNullOrWhiteSpace(actionText)
            ? "Ask me each time"
            : actionText;
    }

    public void OrganizeSelectedFolder()
    {
        OrganizeFiles();
    }

    public void UndoLastOperation()
    {
        UndoOperation();
    }

    private void LoadFiles(string folderPath)
    {
        _pendingOrganizationPlan = null;
        _pendingOrganizationFolderPath = null;
        _pendingConflictAction = FileConflictAction.Ask;
        _viewModel.ClearPreview();
        _viewModel.Files.Clear();

        try
        {
            var files = _fileService.GetFiles(folderPath);
            foreach (var file in files)
            {
                _viewModel.Files.Add(new FileDisplayItem
                {
                    Name = file.Name,
                    FileType = file.FileType,
                    Size = FormatFileSize(file.SizeInBytes),
                    Modified = file.Modified.ToString("yyyy-MM-dd HH:mm")
                });
            }

            _viewModel.FileCountText = _viewModel.Files.Count == 1
                ? "1 file"
                : $"{_viewModel.Files.Count} files";
        }
        catch (Exception ex)
        {
            _viewModel.Files.Clear();
            _viewModel.FileCountText = "0 files";
            MessageBox.Show(
                $"Unable to load files from the selected folder.\n\n{ex.Message}",
                "Unable to Load Files", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OrganizeFiles()
    {
        string folderPath = _viewModel.SelectedFolderPath;
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            MessageBox.Show("Please select a folder first.", "No Folder Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var files = _fileService.GetFiles(folderPath);
            if (files.Count == 0)
            {
                MessageBox.Show("There are no files to organize.", "Nothing to Organize",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var plan = _organizationPlanner.CreatePlan(folderPath, files);
            _pendingConflictAction = GetSelectedConflictAction();

            if (_pendingConflictAction == FileConflictAction.Rename)
            {
                foreach (var item in plan)
                {
                    if (File.Exists(item.DestinationPath) || Directory.Exists(item.DestinationPath))
                    {
                        item.DestinationPath = GetUniqueDestinationPath(item.DestinationPath);
                    }
                }
            }

            _pendingOrganizationPlan = plan;
            _pendingOrganizationFolderPath = Path.GetFullPath(folderPath);
            _viewModel.IsConflictActionEnabled = false;
            _viewModel.PreviewRows.Clear();

            foreach (var item in plan)
            {
                _viewModel.PreviewRows.Add(new OrganizationPreviewRow
                {
                    FileName = item.File.Name,
                    Category = item.Category.ToString(),
                    DestinationPath = item.DestinationPath
                });
            }

            _viewModel.IsPreviewVisible = true;
            _viewModel.ResultsTitle = "Organization Preview";
            _viewModel.ResultsSubtitle = "Review each planned destination before moving files";
            _viewModel.FileCountText = $"{plan.Count} planned";
            _viewModel.PreviewSummary = $"{plan.Count} file(s) planned. Review the destination paths, then execute or cancel the operation.";
        }
        catch (Exception ex)
        {
            _pendingOrganizationPlan = null;
            _pendingOrganizationFolderPath = null;
            _pendingConflictAction = FileConflictAction.Ask;
            _viewModel.ClearPreview();
            MessageBox.Show($"Unable to create the organization preview.\n\n{ex.Message}",
                "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteOrganizationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingOrganizationPlan == null || string.IsNullOrWhiteSpace(_pendingOrganizationFolderPath))
        {
            MessageBox.Show("There is no organization preview to execute.", "No Preview",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string folderPath = _pendingOrganizationFolderPath;
        if (!Directory.Exists(_viewModel.SelectedFolderPath) ||
            !string.Equals(Path.GetFullPath(_viewModel.SelectedFolderPath), folderPath,
                StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("The selected folder has changed. Create a new preview before organizing.",
                "Preview Is Out of Date", MessageBoxButton.OK, MessageBoxImage.Warning);
            LoadFiles(_viewModel.SelectedFolderPath);
            return;
        }

        try
        {
            _applyConflictActionToRemaining = false;
            _rememberedConflictAction = FileConflictAction.Skip;
            var result = _organizationService.Organize(
                folderPath, _pendingOrganizationPlan, _pendingConflictAction, ResolveFileConflict);
            ShowOrganizationResult(result);
            LoadFiles(folderPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to complete the organization operation.\n\n{ex.Message}",
                "Organization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.SelectedFolderPath) &&
            Directory.Exists(_viewModel.SelectedFolderPath))
        {
            LoadFiles(_viewModel.SelectedFolderPath);
            return;
        }

        _pendingOrganizationPlan = null;
        _pendingOrganizationFolderPath = null;
        _pendingConflictAction = FileConflictAction.Ask;
        _viewModel.ClearPreview();
        _viewModel.Files.Clear();
        _viewModel.FileCountText = "0 files";
    }

    private void UndoOperation()
    {
        string folderPath = _viewModel.SelectedFolderPath;
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            MessageBox.Show("Please select a folder first.", "No Folder Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var operations = _undoRepository.GetLatestOperation(folderPath);
        if (operations.Count == 0)
        {
            MessageBox.Show("There is nothing to undo for this folder.", "Undo",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            $"The last organization operation for this folder contains {operations.Count} file(s).\n\nDo you want to undo it?",
            "Confirm Undo", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _undoService.Undo(operations);
        ShowUndoResult(result);
        LoadFiles(folderPath);
    }

    private FileConflictAction GetSelectedConflictAction() => _viewModel.SelectedConflictAction switch
    {
        "Rename incoming file" => FileConflictAction.Rename,
        "Skip incoming file" => FileConflictAction.Skip,
        "Replace existing file" => FileConflictAction.Replace,
        _ => FileConflictAction.Ask
    };

    private FileConflictAction ResolveFileConflict(OrganizationItem item, string destinationPath)
    {
        if (_applyConflictActionToRemaining)
        {
            return _rememberedConflictAction;
        }

        var dialog = new Window
        {
            Title = "Destination File Already Exists",
            Width = 610, Height = 310, MinWidth = 610, MinHeight = 310,
            MaxWidth = 610, MaxHeight = 310, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this), ShowInTaskbar = false,
            Background = SystemColors.WindowBrush, Foreground = SystemColors.WindowTextBrush
        };
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = $"A file already exists at the planned destination for \"{item.File.Name}\".",
            TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 10)
        });
        root.Children.Add(new TextBlock
        {
            Text = destinationPath, TextWrapping = TextWrapping.Wrap, FontSize = 12,
            Margin = new Thickness(0, 0, 0, 14)
        });
        var applyToAllCheckBox = new CheckBox
        {
            Content = "Do this for all remaining conflicts",
            Margin = new Thickness(0, 0, 0, 18), FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        root.Children.Add(applyToAllCheckBox);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right
        };
        FileConflictAction selectedAction = FileConflictAction.Cancel;
        void AddActionButton(string caption, FileConflictAction action)
        {
            var button = new Button
            {
                Content = caption, MinWidth = caption == "Cancel remaining" ? 118 : 82,
                Height = 36, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0)
            };
            button.Click += (_, _) => { selectedAction = action; dialog.DialogResult = true; };
            buttons.Children.Add(button);
        }
        AddActionButton("Rename", FileConflictAction.Rename);
        AddActionButton("Skip", FileConflictAction.Skip);
        AddActionButton("Replace", FileConflictAction.Replace);
        AddActionButton("Cancel remaining", FileConflictAction.Cancel);
        root.Children.Add(buttons);
        dialog.Content = root;
        dialog.ShowDialog();

        if (applyToAllCheckBox.IsChecked == true &&
            selectedAction is FileConflictAction.Rename or FileConflictAction.Skip or FileConflictAction.Replace)
        {
            _rememberedConflictAction = selectedAction;
            _applyConflictActionToRemaining = true;
        }

        return selectedAction;
    }

    private static string GetUniqueDestinationPath(string destinationPath)
    {
        string directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The destination folder could not be determined.");
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(destinationPath);
        string extension = Path.GetExtension(destinationPath);
        int counter = 1;
        string candidatePath;
        do
        {
            candidatePath = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter}){extension}");
            counter++;
        }
        while (File.Exists(candidatePath) || Directory.Exists(candidatePath));
        return candidatePath;
    }

    private static void ShowOrganizationResult(OrganizationResult result)
    {
        var message = $"Moved: {result.MovedFiles.Count} files";
        if (result.SkippedFiles.Count > 0) message += $"\nSkipped: {result.SkippedFiles.Count} files";
        if (result.FailedFiles.Count > 0) message += $"\nFailed: {result.FailedFiles.Count} files";
        MessageBox.Show(message, "Organization Complete", MessageBoxButton.OK,
            result.FailedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private static void ShowUndoResult(UndoResult result)
    {
        var message = $"Restored: {result.RestoredFiles.Count} files";
        if (result.SkippedFiles.Count > 0) message += $"\nSkipped: {result.SkippedFiles.Count} files";
        if (result.FailedFiles.Count > 0) message += $"\nFailed: {result.FailedFiles.Count} files";
        MessageBox.Show(message, "Undo Complete", MessageBoxButton.OK,
            result.FailedFiles.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
