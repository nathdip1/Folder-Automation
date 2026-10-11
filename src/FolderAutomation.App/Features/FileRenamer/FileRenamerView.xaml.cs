
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FolderAutomation.Core;
using FolderAutomation.Core.FileRenamer;

namespace FolderAutomation.Features.FileRenamer;

public partial class FileRenamerView : UserControl
{
    private readonly FileRenamerCoordinator _coordinator;

    private string? _selectedFolder;
    private IReadOnlyList<RenameFileItem> _files = [];
    private IReadOnlyList<RenamePreviewItem> _previewItems = [];

    public FileRenamerView()
    {
        InitializeComponent();

        string applicationDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderAutomation");

        string databasePath = Path.Combine(
            applicationDataFolder,
            "FolderAutomation.db");

        _coordinator = new FileRenamerCoordinator(databasePath);

        ConfigureFileLists();
        ConfigurePreviewList();
        ConfigureHistoryList();

        RenameSelectedButton.IsEnabled = false;
        UndoRenameButton.IsEnabled = false;

        RenameStatusText.Text =
            "Select a folder to load its files and saved rename history.";
    }

    private void ConfigureFileLists()
    {
        ConfigureCategoryList(PhotosItemsControl);
        ConfigureCategoryList(VideosItemsControl);
        ConfigureCategoryList(PdfsItemsControl);
        ConfigureCategoryList(WordDocumentsItemsControl);
        ConfigureCategoryList(MusicItemsControl);
        ConfigureCategoryList(ExcelItemsControl);
        ConfigureCategoryList(OtherItemsControl);
    }

    private void ConfigureCategoryList(ItemsControl itemsControl)
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));

        var checkBox = new FrameworkElementFactory(typeof(CheckBox));
        checkBox.SetBinding(
            CheckBox.ContentProperty,
            new System.Windows.Data.Binding(nameof(RenameFileItem.Name)));
        checkBox.SetBinding(
            CheckBox.IsCheckedProperty,
            new System.Windows.Data.Binding(nameof(RenameFileItem.IsSelected))
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger =
                    System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
        checkBox.SetValue(CheckBox.MarginProperty, new Thickness(0, 3, 0, 3));
        checkBox.SetValue(CheckBox.ToolTipProperty,
            new System.Windows.Data.Binding(nameof(RenameFileItem.FullPath)));

        panel.AppendChild(checkBox);

        itemsControl.ItemTemplate = new DataTemplate
        {
            VisualTree = panel
        };
    }

    private void ConfigurePreviewList()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));

        var originalPath = new FrameworkElementFactory(typeof(TextBlock));
        originalPath.SetBinding(
            TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(RenamePreviewItem.OriginalPath)));
        originalPath.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        originalPath.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        originalPath.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 2));

        var proposedPath = new FrameworkElementFactory(typeof(TextBlock));
        proposedPath.SetBinding(
            TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(RenamePreviewItem.ProposedPath)));
        proposedPath.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        proposedPath.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 6));

        var validation = new FrameworkElementFactory(typeof(TextBlock));
        validation.SetBinding(
            TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(RenamePreviewItem.ValidationMessage)));
        validation.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        validation.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 8));

        panel.AppendChild(originalPath);
        panel.AppendChild(proposedPath);
        panel.AppendChild(validation);

        RenamePreviewItemsControl.ItemTemplate = new DataTemplate
        {
            VisualTree = panel
        };
    }

    private void ConfigureHistoryList()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));

        var originalPath = new FrameworkElementFactory(typeof(TextBlock));
        originalPath.SetBinding(
            TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(RenameHistoryEntry.OriginalPath)));
        originalPath.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        originalPath.SetValue(TextBlock.MarginProperty, new Thickness(0, 3, 0, 0));

        var newPath = new FrameworkElementFactory(typeof(TextBlock));
        newPath.SetBinding(
            TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(RenameHistoryEntry.NewPath)));
        newPath.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        newPath.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 8));

        panel.AppendChild(originalPath);
        panel.AppendChild(newPath);

        RenameHistoryItemsControl.ItemTemplate = new DataTemplate
        {
            VisualTree = panel
        };
    }

    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to rename files",
            Multiselect = false
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        _selectedFolder = dialog.FolderName;

        SelectedFolderText.Text = _selectedFolder;

        LoadFolderContents();
    }

    private void LoadFolderContents()
    {
        if (string.IsNullOrWhiteSpace(_selectedFolder))
            return;

        try
        {
            _files = _coordinator.LoadFiles(_selectedFolder);
            _previewItems = [];

            BindCategory(PhotosItemsControl, RenameFileCategory.Photos);
            BindCategory(VideosItemsControl, RenameFileCategory.Videos);
            BindCategory(PdfsItemsControl, RenameFileCategory.Pdfs);
            BindCategory(WordDocumentsItemsControl, RenameFileCategory.WordDocuments);
            BindCategory(MusicItemsControl, RenameFileCategory.Music);
            BindCategory(ExcelItemsControl, RenameFileCategory.Excel);
            BindCategory(OtherItemsControl, RenameFileCategory.Other);

            RenamePreviewItemsControl.ItemsSource = null;

            FileCountText.Text = $"{_files.Count} files found in the selected folder.";

            LoadHistory();

            RenameSelectedButton.IsEnabled = false;

            RenameStatusText.Text =
                "Select files, enter a base name for each selected category, and preview the proposed names.";
        }
        catch (Exception ex)
        {
            ShowError("Could not load the selected folder.", ex);
        }
    }

    private void BindCategory(
        ItemsControl itemsControl,
        RenameFileCategory category)
    {
        itemsControl.ItemsSource = _files
            .Where(file => file.Category == category)
            .ToList();
    }

    private void LoadHistory()
    {
        if (string.IsNullOrWhiteSpace(_selectedFolder))
            return;

        try
        {
            var history = _coordinator.GetOperations(_selectedFolder);

            RenameHistoryItemsControl.ItemsSource = history;

            UndoRenameButton.IsEnabled = history.Count > 0;

            if (history.Count == 0)
            {
                RenameStatusText.Text =
                    "No saved, undoable rename history exists for this folder. Existing filenames will not be guessed.";
            }
        }
        catch (Exception ex)
        {
            UndoRenameButton.IsEnabled = false;
            ShowError("Could not load saved rename history.", ex);
        }
    }

    private void SelectAllFilesButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelection(_files, true);
        RefreshCategoryLists();
    }

    private void CategorySelectAll_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
            return;

        string? categoryName = checkBox.Tag?.ToString();

        if (!Enum.TryParse(categoryName, out RenameFileCategory category))
            return;

        SetSelection(_files.Where(file => file.Category == category), true);
        RefreshCategoryLists();
    }

    private void CategorySelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
            return;

        string? categoryName = checkBox.Tag?.ToString();

        if (!Enum.TryParse(categoryName, out RenameFileCategory category))
            return;

        SetSelection(_files.Where(file => file.Category == category), false);
        RefreshCategoryLists();
    }

    private static void SetSelection(
        IEnumerable<RenameFileItem> files,
        bool selected)
    {
        foreach (RenameFileItem file in files)
            file.IsSelected = selected;
    }

    private void RefreshCategoryLists()
    {
        PhotosItemsControl.Items.Refresh();
        VideosItemsControl.Items.Refresh();
        PdfsItemsControl.Items.Refresh();
        WordDocumentsItemsControl.Items.Refresh();
        MusicItemsControl.Items.Refresh();
        ExcelItemsControl.Items.Refresh();
        OtherItemsControl.Items.Refresh();
    }

    private void PreviewRenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedFolder))
        {
            MessageBox.Show(
                Window.GetWindow(this),
                "Select a folder first.",
                "File Renamer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        List<RenameFileItem> selectedFiles =
            _files.Where(file => file.IsSelected).ToList();

        if (selectedFiles.Count == 0)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                "Select at least one file to rename.",
                "File Renamer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var baseNames = new Dictionary<RenameFileCategory, string>();

        AddBaseName(baseNames, RenameFileCategory.Photos, PhotosBaseNameTextBox);
        AddBaseName(baseNames, RenameFileCategory.Videos, VideosBaseNameTextBox);
        AddBaseName(baseNames, RenameFileCategory.Pdfs, PdfsBaseNameTextBox);
        AddBaseName(baseNames, RenameFileCategory.WordDocuments, WordDocumentsBaseNameTextBox);
        AddBaseName(baseNames, RenameFileCategory.Music, MusicBaseNameTextBox);
        AddBaseName(baseNames, RenameFileCategory.Excel, ExcelBaseNameTextBox);
        AddBaseName(baseNames, RenameFileCategory.Other, OtherBaseNameTextBox);

        try
        {
            _previewItems = _coordinator.CreatePreview(selectedFiles, baseNames);
            RenamePreviewItemsControl.ItemsSource = _previewItems;

            int invalidCount = _previewItems.Count(item => !item.IsValid);

            RenameSelectedButton.IsEnabled =
                invalidCount == 0 && _previewItems.Count > 0;

            RenameStatusText.Text =
                $"Preview ready: {_previewItems.Count} file(s); {invalidCount} issue(s). No files have been changed.";

            if (invalidCount > 0)
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    "Some proposed names are invalid or conflict with existing files. Review the preview and correct the issues before renaming.",
                    "Preview contains conflicts",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            RenameSelectedButton.IsEnabled = false;
            ShowError("Could not generate the rename preview.", ex);
        }
    }

    private static void AddBaseName(
        IDictionary<RenameFileCategory, string> baseNames,
        RenameFileCategory category,
        TextBox textBox)
    {
        baseNames[category] = textBox.Text.Trim();
    }

    private void RenameSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_previewItems.Count == 0 || _previewItems.Any(item => !item.IsValid))
        {
            MessageBox.Show(
                Window.GetWindow(this),
                "Generate a valid preview before confirming the rename.",
                "File Renamer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            Window.GetWindow(this),
            $"Rename {_previewItems.Count} file(s) using the previewed names?\n\n" +
            "Files will not be overwritten intentionally. The application will save undo history to SQLite.",
            "Confirm Rename",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
            return;

        RenameSelectedButton.IsEnabled = false;
        PreviewRenameButton.IsEnabled = false;

        try
        {
            RenameOperationResult result = _coordinator.ExecuteRename(_previewItems);

            string summary =
                $"Renamed: {result.RenamedFiles.Count}\n" +
                $"Skipped: {result.SkippedFiles.Count}\n" +
                $"Failed: {result.FailedFiles.Count}";

            if (result.FailedFiles.Count > 0)
                summary += "\n\n" + string.Join("\n", result.FailedFiles);

            RenameStatusText.Text = summary;

            if (result.HasChanges)
            {
                LoadFolderContents();
                LoadHistory();
            }

            MessageBox.Show(
                Window.GetWindow(this),
                summary,
                "File Renamer Results",
                MessageBoxButton.OK,
                result.FailedFiles.Count > 0
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("The rename operation did not complete normally.", ex);
        }
        finally
        {
            PreviewRenameButton.IsEnabled = true;
        }
    }

    private void UndoRenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedFolder))
        {
            MessageBox.Show(
                Window.GetWindow(this),
                "Select the folder whose rename history you want to undo.",
                "Undo Rename",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            Window.GetWindow(this),
            "Undo the most recent saved rename operation for this folder?\n\n" +
            "The application will check the current files and will not intentionally overwrite existing files.",
            "Confirm Undo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
            return;

        UndoRenameButton.IsEnabled = false;

        try
        {
            UndoResult result = _coordinator.UndoLatestOperation(_selectedFolder);

            string summary =
                $"Restored: {result.RestoredFiles.Count}\n" +
                $"Skipped: {result.SkippedFiles.Count}\n" +
                $"Failed: {result.FailedFiles.Count}";

            if (result.SkippedFiles.Count > 0)
                summary += "\n\nSkipped:\n" + string.Join("\n", result.SkippedFiles);

            if (result.FailedFiles.Count > 0)
                summary += "\n\nErrors:\n" + string.Join("\n", result.FailedFiles);

            RenameStatusText.Text = summary;

            if (result.RestoredFiles.Count > 0)
            {
                LoadFolderContents();
                LoadHistory();
            }

            MessageBox.Show(
                Window.GetWindow(this),
                summary,
                "Undo Rename Results",
                MessageBoxButton.OK,
                result.FailedFiles.Count > 0
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Could not undo the saved rename operation.", ex);
        }
        finally
        {
            LoadHistory();
        }
    }

    private void ShowError(string message, Exception exception)
    {
        RenameStatusText.Text = message;

        MessageBox.Show(
            Window.GetWindow(this),
            $"{message}\n\n{exception.Message}",
            "File Renamer Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
