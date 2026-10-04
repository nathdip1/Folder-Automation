using System.Windows;
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

        string applicationDataFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "FolderAutomation");

        string databasePath = System.IO.Path.Combine(
            applicationDataFolder,
            "FolderAutomation.db");

        var databaseConnection = new DatabaseConnection(databasePath);

        _undoRepository = new UndoRepository(databaseConnection);

        _undoRepository.CreateTable();

        _organizationService = new OrganizationService(
            _undoRepository);

        _undoService = new UndoService();
    }

    private void SelectFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a folder"
        };

        if (dialog.ShowDialog() == true)
        {
            string selectedFolder = dialog.FolderName;

            SelectedFolderText.Text = selectedFolder;

            LoadFiles(selectedFolder);
        }
    }

    private void LoadFiles(string folderPath)
    {
        var files = _fileService.GetFiles(folderPath);

        var displayItems = files.Select(file => new FileDisplayItem
        {
            Name = file.Name,
            FileType = file.FileType,
            Size = FormatFileSize(file.SizeInBytes),
            Modified = file.Modified.ToString("yyyy-MM-dd HH:mm")
        }).ToList();

        FilesDataGrid.ItemsSource = displayItems;

        FileCountText.Text = displayItems.Count == 1
            ? "1 file"
            : $"{displayItems.Count} files";
    }

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

        LoadFiles(folderPath);
    }

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

        LoadFiles(folderPath);
    }

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

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";

        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";

        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}

public class FileDisplayItem
{
    public string Name { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string Size { get; set; } = string.Empty;

    public string Modified { get; set; } = string.Empty;
}