using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FolderAutomation.Core;
using FolderAutomation.Data;

namespace FolderAutomation;

public partial class MainWindow : Window
{
    private readonly FileService _fileService;
    private readonly OrganizationPlanner _organizationPlanner;
    private readonly OrganizationService _organizationService;
    private readonly UndoRepository _undoRepository;
    private readonly UndoService _undoService;

    public MainWindow()
    {
        InitializeComponent();

        _fileService = new FileService();
        _organizationPlanner = new OrganizationPlanner();

        string applicationDataFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "FolderAutomation");

        string databasePath = Path.Combine(
            applicationDataFolder,
            "FolderAutomation.db");

        var databaseConnection = new DatabaseConnection(databasePath);

        _undoRepository = new UndoRepository(databaseConnection);

        _undoRepository.CreateTable();

        _organizationService = new OrganizationService(
            _undoRepository);

        _undoService = new UndoService();
    }


    // ============================================================
    // FEATURE ACCORDION
    // ============================================================

    private void FeatureExpander_Expanded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Expander expandedExpander)
            return;

        foreach (var expander in FindVisualChildren<Expander>(this))
        {
            if (!ReferenceEquals(expander, expandedExpander))
            {
                expander.IsExpanded = false;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(
        DependencyObject dependencyObject)
        where T : DependencyObject
    {
        if (dependencyObject == null)
            yield break;

        int childCount =
            VisualTreeHelper.GetChildrenCount(dependencyObject);

        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    dependencyObject,
                    i);

            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }


    // ============================================================
    // SELECT FOLDER
    // ============================================================

    private void SelectFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a folder"
        };

        // Open the native Windows folder picker.
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string selectedFolder = dialog.FolderName;

        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        // Update the selected folder displayed in the UI.
        SelectedFolderText.Text = selectedFolder;

        // Load the files from the selected folder.
        LoadFiles(selectedFolder);
    }


    // ============================================================
    // LOAD FILES
    // ============================================================

    private void LoadFiles(string folderPath)
    {
        try
        {
            var files = _fileService.GetFiles(folderPath);

            var displayItems = files
                .Select(file => new FileDisplayItem
                {
                    Name = file.Name,
                    FileType = file.FileType,
                    Size = FormatFileSize(file.SizeInBytes),
                    Modified = file.Modified.ToString("yyyy-MM-dd HH:mm")
                })
                .ToList();

            FilesDataGrid.ItemsSource = displayItems;

            FileCountText.Text = displayItems.Count == 1
                ? "1 file"
                : $"{displayItems.Count} files";
        }
        catch (Exception ex)
        {
            FilesDataGrid.ItemsSource = null;
            FileCountText.Text = "0 files";

            MessageBox.Show(
                $"Unable to load files from the selected folder.\n\n{ex.Message}",
                "Unable to Load Files",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }


    // ============================================================
    // ORGANIZE FILES
    // ============================================================

    private void OrganizeFilesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderText.Text) ||
            SelectedFolderText.Text == "No folder selected")
        {
            MessageBox.Show(
                "Please select a folder first.",
                "No Folder Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        string folderPath = SelectedFolderText.Text;

        var files = _fileService.GetFiles(folderPath);

        if (files.Count == 0)
        {
            MessageBox.Show(
                "There are no files to organize.",
                "Nothing to Organize",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var plan = _organizationPlanner.CreatePlan(
            folderPath,
            files);

        var summary = plan
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key.ToString())
            .Select(group =>
                $"{group.Key}: {group.Count()} files")
            .ToList();

        string previewMessage =
            "The following files will be organized:\n\n" +
            string.Join("\n", summary) +
            "\n\nDo you want to continue?";

        MessageBoxResult confirmation = MessageBox.Show(
            previewMessage,
            "Organization Preview",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _organizationService.Organize(
            folderPath,
            plan);

        ShowOrganizationResult(result);

        // Refresh the file list after organization.
        LoadFiles(folderPath);
    }


    // ============================================================
    // UNDO
    // ============================================================

    private void UndoButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderText.Text) ||
            SelectedFolderText.Text == "No folder selected")
        {
            MessageBox.Show(
                "Please select a folder first.",
                "No Folder Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        string folderPath = SelectedFolderText.Text;

        var operations =
            _undoRepository.GetLatestOperation(folderPath);

        if (operations.Count == 0)
        {
            MessageBox.Show(
                "There is nothing to undo for this folder.",
                "Undo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            $"The last organization operation for this folder " +
            $"contains {operations.Count} file(s).\n\n" +
            "Do you want to undo it?",
            "Confirm Undo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _undoService.Undo(operations);

        ShowUndoResult(result);

        // Refresh the file list after undo.
        LoadFiles(folderPath);
    }


    // ============================================================
    // ORGANIZATION RESULT
    // ============================================================

    private void ShowOrganizationResult(
        OrganizationResult result)
    {
        var message =
            $"Moved: {result.MovedFiles.Count} files";

        if (result.SkippedFiles.Count > 0)
        {
            message +=
                $"\nSkipped: {result.SkippedFiles.Count} files";
        }

        if (result.FailedFiles.Count > 0)
        {
            message +=
                $"\nFailed: {result.FailedFiles.Count} files";
        }

        MessageBox.Show(
            message,
            "Organization Complete",
            MessageBoxButton.OK,
            result.FailedFiles.Count > 0
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information);
    }


    // ============================================================
    // UNDO RESULT
    // ============================================================

    private void ShowUndoResult(
        UndoResult result)
    {
        var message =
            $"Restored: {result.RestoredFiles.Count} files";

        if (result.SkippedFiles.Count > 0)
        {
            message +=
                $"\nSkipped: {result.SkippedFiles.Count} files";
        }

        if (result.FailedFiles.Count > 0)
        {
            message +=
                $"\nFailed: {result.FailedFiles.Count} files";
        }

        MessageBox.Show(
            message,
            "Undo Complete",
            MessageBoxButton.OK,
            result.FailedFiles.Count > 0
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information);
    }


    // ============================================================
    // FILE SIZE FORMATTING
    // ============================================================

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        if (bytes < 1024 * 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}


// ================================================================
// FILE DISPLAY MODEL
// ================================================================

public class FileDisplayItem
{
    public string Name { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string Size { get; set; } = string.Empty;

    public string Modified { get; set; } = string.Empty;
}