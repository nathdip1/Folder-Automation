using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FolderAutomation.Controls.FolderBrowser;

public partial class FolderBrowserWindow : Window
{
    public string? SelectedFolder { get; private set; }

    public FolderBrowserWindow()
    {
        InitializeComponent();

        LoadDrives();
    }

    private void LoadDrives()
    {
        FolderTree.Items.Clear();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                    continue;

                var driveName = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.Name
                    : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";

                var node = new FolderNode(
                    driveName,
                    drive.RootDirectory.FullName);

                var item = CreateTreeItem(node);

                FolderTree.Items.Add(item);
            }
            catch
            {
                // Ignore drives that cannot be accessed.
            }
        }
    }

    private TreeViewItem CreateTreeItem(FolderNode node)
    {
        var item = new TreeViewItem
        {
            Header = $"📁 {node.Name}",
            Tag = node
        };

        // Attach the expansion handler to this individual TreeViewItem.
        item.Expanded += FolderTree_Expanded;

        // Add a temporary child so WPF displays the expand arrow.
        if (HasSubDirectories(node.FullPath))
        {
            item.Items.Add(new TreeViewItem
            {
                Header = "Loading..."
            });
        }

        return item;
    }

    private static bool HasSubDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).Any();
        }
        catch
        {
            return false;
        }
    }

    private static List<FolderNode> GetSubDirectories(string path)
    {
        var result = new List<FolderNode>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                try
                {
                    result.Add(
                        new FolderNode(
                            Path.GetFileName(directory),
                            directory));
                }
                catch
                {
                    // Ignore invalid directory entries.
                }
            }
        }
        catch
        {
            // Access denied or unavailable directory.
        }

        return result
            .OrderBy(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void FolderTree_Expanded(
        object sender,
        RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item)
            return;

        if (item.Tag is not FolderNode node)
            return;

        // Already populated.
        if (item.Items.Count != 1 ||
            item.Items[0] is not TreeViewItem placeholder ||
            placeholder.Header?.ToString() != "Loading...")
        {
            return;
        }

        // Remove the temporary Loading... item.
        item.Items.Clear();

        // Load the actual child folders.
        foreach (var child in GetSubDirectories(node.FullPath))
        {
            item.Items.Add(CreateTreeItem(child));
        }
    }

    private void FolderTree_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item)
            return;

        if (item.Tag is not FolderNode node)
            return;

        SelectedFolder = node.FullPath;

        SelectedPathText.Text = node.FullPath;
    }

    private void SelectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            MessageBox.Show(
                "Please select a folder first.",
                "No Folder Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}