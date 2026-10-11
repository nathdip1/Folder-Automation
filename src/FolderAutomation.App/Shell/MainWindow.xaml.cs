
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

using FolderAutomation.Features.FolderOrganizer;

namespace FolderAutomation;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void FeatureExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander expandedExpander)
            return;

        // Collapse every other feature in the sidebar.
        foreach (Expander expander in FindVisualChildren<Expander>(this))
        {
            if (!ReferenceEquals(expander, expandedExpander))
                expander.IsExpanded = false;
        }

        // Hide all feature views before showing the selected view.
        FolderOrganizerViewControl.Visibility = Visibility.Collapsed;
        FileRenamerViewControl.Visibility = Visibility.Collapsed;
        FileConverterViewControl.Visibility = Visibility.Collapsed;
        FileCleanerViewControl.Visibility = Visibility.Collapsed;

        // Display the view corresponding to the selected feature.
        if (expandedExpander.Name == nameof(FolderOrganizerExpander))
        {
            FolderOrganizerViewControl.Visibility = Visibility.Visible;
        }
        else if (expandedExpander.Name == nameof(FileRenamerExpander))
        {
            FileRenamerViewControl.Visibility = Visibility.Visible;
        }
        else if (string.Equals(
                     expandedExpander.Header?.ToString(),
                     "File Converter",
                     StringComparison.Ordinal))
        {
            FileConverterViewControl.Visibility = Visibility.Visible;
        }
        else if (string.Equals(
                     expandedExpander.Header?.ToString(),
                     "File Cleaner",
                     StringComparison.Ordinal))
        {
            FileCleanerViewControl.Visibility = Visibility.Visible;
        }
    }

    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select a folder to organize"
        };

        if (folderDialog.ShowDialog(this) != true)
            return;

        string selectedFolder = folderDialog.FolderName;

        SelectedFolderText.Text = selectedFolder;

        FolderOrganizerViewControl.SetSelectedFolder(selectedFolder);
    }

    private void OrganizeFilesButton_Click(object sender, RoutedEventArgs e)
    {
        string conflictAction =
            (ConflictActionComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? "Ask me each time";

        FolderOrganizerViewControl.SetConflictAction(conflictAction);
        FolderOrganizerViewControl.OrganizeSelectedFolder();
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        FolderOrganizerViewControl.UndoLastOperation();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        if (parent == null)
            yield break;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T matchingChild)
                yield return matchingChild;

            foreach (T descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
