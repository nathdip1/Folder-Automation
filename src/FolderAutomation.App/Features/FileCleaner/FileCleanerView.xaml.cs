using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Runtime.InteropServices;

using System.Security;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Media;

using System.Windows.Data;

namespace FolderAutomation.Features.FileCleaner;

public partial class FileCleanerView : UserControl

{

    private static readonly TimeSpan MinimumFileAge =

        TimeSpan.FromHours(24);

    private const uint RecycleBinNoConfirmation = 0x00000001;

    private const uint RecycleBinNoProgressUi = 0x00000002;

    private const uint RecycleBinNoSound = 0x00000004;

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]

    private static extern int SHEmptyRecycleBin(

        IntPtr hwnd,

        string? rootPath,

        uint flags);

    public FileCleanerView()

    {

        InitializeComponent();

    }

    private void ScanTemporaryFilesButton_Click(

        object sender,

        RoutedEventArgs e)

    {

        ScanTemporaryFilesButton.IsEnabled = false;

        CleanerStatusText.Text = "Scanning temporary folders. Please wait.";

        try

        {

            List<string> roots = GetTemporaryFolders();

            var eligibleFiles = new List<TemporaryFileInfo>();

            int skippedCount = 0;

            foreach (string root in roots)

            {

                ScanDirectory(root, eligibleFiles, ref skippedCount);

            }

            long totalBytes = eligibleFiles.Sum(file => file.SizeBytes);

            CleanerStatusText.Text =

                $"Scan complete. {eligibleFiles.Count:N0} eligible files found; " +

                $"{FormatFileSize(totalBytes)} total. " +

                $"{skippedCount:N0} items or locations skipped. No files were deleted.";

            ShowScanResults(eligibleFiles, skippedCount, totalBytes);

        }

        catch (Exception ex)

        {

            CleanerStatusText.Text = "The scan could not be completed.";

            MessageBox.Show(

                Window.GetWindow(this),

                $"The temporary-file scan could not be completed.\n\n{ex.Message}",

                "File Cleaner - Scan Error",

                MessageBoxButton.OK,

                MessageBoxImage.Error);

        }

        finally

        {

            ScanTemporaryFilesButton.IsEnabled = true;

        }

    }

    private static List<string> GetTemporaryFolders()

    {

        var roots = new List<string>();

        string userTemp = Path.GetTempPath();

        if (!string.IsNullOrWhiteSpace(userTemp))

        {

            roots.Add(Path.GetFullPath(userTemp));

        }

        string windowsDirectory = Environment.GetFolderPath(

            Environment.SpecialFolder.Windows);

        if (!string.IsNullOrWhiteSpace(windowsDirectory))

        {

            roots.Add(Path.GetFullPath(

                Path.Combine(windowsDirectory, "Temp")));

        }

        return roots

            .Distinct(StringComparer.OrdinalIgnoreCase)

            .ToList();

    }

    private static void ScanDirectory(

        string root,

        List<TemporaryFileInfo> eligibleFiles,

        ref int skippedCount)

    {

        if (!Directory.Exists(root))

        {

            skippedCount++;

            return;

        }

        var pendingDirectories = new Stack<string>();

        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)

        {

            string currentDirectory = pendingDirectories.Pop();

            try

            {

                foreach (string filePath in

                         Directory.EnumerateFiles(currentDirectory))

                {

                    try

                    {

                        var fileInfo = new FileInfo(filePath);

                        if (!fileInfo.Exists ||

                            (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0 ||

                            DateTime.Now - fileInfo.LastWriteTime < MinimumFileAge)

                        {

                            skippedCount++;

                            continue;

                        }

                        // Best-effort check for files currently in use.

                        using (new FileStream(

                                   filePath,

                                   FileMode.Open,

                                   FileAccess.Read,

                                   FileShare.None))

                        {

                        }

                        eligibleFiles.Add(new TemporaryFileInfo(

                            fileInfo.FullName,

                            fileInfo.Length,

                            fileInfo.LastWriteTime));

                    }

                    catch (UnauthorizedAccessException)

                    {

                        skippedCount++;

                    }

                    catch (IOException)

                    {

                        skippedCount++;

                    }

                    catch (SecurityException)

                    {

                        skippedCount++;

                    }

                }

            }

            catch (UnauthorizedAccessException)

            {

                skippedCount++;

            }

            catch (IOException)

            {

                skippedCount++;

            }

            catch (SecurityException)

            {

                skippedCount++;

            }

            try

            {

                foreach (string subdirectory in

                         Directory.EnumerateDirectories(currentDirectory))

                {

                    try

                    {

                        var directoryInfo = new DirectoryInfo(subdirectory);

                        if ((directoryInfo.Attributes &

                             FileAttributes.ReparsePoint) != 0)

                        {

                            skippedCount++;

                            continue;

                        }

                        pendingDirectories.Push(directoryInfo.FullName);

                    }

                    catch (UnauthorizedAccessException)

                    {

                        skippedCount++;

                    }

                    catch (IOException)

                    {

                        skippedCount++;

                    }

                    catch (SecurityException)

                    {

                        skippedCount++;

                    }

                }

            }

            catch (UnauthorizedAccessException)

            {

                skippedCount++;

            }

            catch (IOException)

            {

                skippedCount++;

            }

            catch (SecurityException)

            {

                skippedCount++;

            }

        }

    }

    private void ShowScanResults(

        List<TemporaryFileInfo> files,

        int initialSkippedCount,

        long initialTotalBytes)

    {

        Window? owner = Window.GetWindow(this);

        var window = new Window

        {

            Title = "Temporary Files - Scan Results",

            Width = 1000,

            Height = 600,

            MinWidth = 700,

            MinHeight = 400,

            WindowStartupLocation = owner == null

                ? WindowStartupLocation.CenterScreen

                : WindowStartupLocation.CenterOwner,

            Owner = owner,

            Background = Brushes.White

        };

        var layout = new Grid
        {
            Margin = new Thickness(18)
        };

        layout.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        layout.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        layout.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        var summary = new TextBlock

        {

            Text =

                $"Eligible files: {files.Count:N0}    " +

                $"Total size: {FormatFileSize(initialTotalBytes)}    " +

                $"Skipped during scan: {initialSkippedCount:N0}\n\n" +

                "Review the list before deleting. Files are rechecked before removal.",

            TextWrapping = TextWrapping.Wrap,

            Margin = new Thickness(0, 0, 0, 14),

            FontSize = 13

        };

        Grid.SetRow(summary, 0);

        layout.Children.Add(summary);

        var resultsGrid = new DataGrid

        {

            AutoGenerateColumns = false,

            IsReadOnly = true,

            CanUserAddRows = false,

            CanUserDeleteRows = false,

            SelectionMode = DataGridSelectionMode.Single,

            HeadersVisibility = DataGridHeadersVisibility.Column,

            ItemsSource = CreateDisplayRows(files)

        };

        resultsGrid.Columns.Add(new DataGridTextColumn

        {

            Header = "File Path",

            Binding = new Binding(nameof(TemporaryFileDisplay.Path)),

            Width = new DataGridLength(1, DataGridLengthUnitType.Star)

        });

        resultsGrid.Columns.Add(new DataGridTextColumn

        {

            Header = "Size",

            Binding = new Binding(nameof(TemporaryFileDisplay.Size)),

            Width = new DataGridLength(120)

        });

        resultsGrid.Columns.Add(new DataGridTextColumn

        {

            Header = "Last Modified",

            Binding = new Binding(nameof(TemporaryFileDisplay.LastModified)),

            Width = new DataGridLength(170)

        });

        Grid.SetRow(resultsGrid, 1);

        layout.Children.Add(resultsGrid);

        var buttonsPanel = new StackPanel

        {

            Orientation = Orientation.Horizontal,

            HorizontalAlignment = HorizontalAlignment.Right,

            Margin = new Thickness(0, 14, 0, 0)

        };

        var deleteButton = new Button

        {

            Content = "Delete Files",

            MinWidth = 120,

            Height = 38,

            Padding = new Thickness(16, 0, 16, 0),

            Margin = new Thickness(0, 0, 10, 0),

            Background = new SolidColorBrush(Color.FromRgb(190, 45, 45)),

            Foreground = Brushes.White,

            BorderBrush = new SolidColorBrush(Color.FromRgb(190, 45, 45)),

            IsEnabled = files.Count > 0

        };

        var closeButton = new Button

        {

            Content = "Close",

            Width = 100,

            Height = 38,

            IsCancel = true

        };

        deleteButton.Click += (_, _) =>

        {

            if (files.Count == 0)

            {

                MessageBox.Show(

                    window,

                    "There are no remaining files to delete.",

                    "File Cleaner",

                    MessageBoxButton.OK,

                    MessageBoxImage.Information);

                return;

            }

            bool? alsoEmptyRecycleBin = ShowDeleteConfirmation(window);

            if (alsoEmptyRecycleBin == null)

                return;

            deleteButton.IsEnabled = false;

            closeButton.IsEnabled = false;

            ScanTemporaryFilesButton.IsEnabled = false;

            try

            {

                int deletedCount = 0;

                int skippedCount = 0;

                int failedCount = 0;

                long deletedBytes = 0;

                var remainingFiles = new List<TemporaryFileInfo>();

                foreach (TemporaryFileInfo file in files)

                {

                    try

                    {

                        if (!IsApprovedTemporaryFile(file.Path))

                        {

                            skippedCount++;

                            remainingFiles.Add(file);

                            continue;

                        }

                        var fileInfo = new FileInfo(file.Path);

                        if (!fileInfo.Exists ||

                            (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0 ||

                            DateTime.Now - fileInfo.LastWriteTime < MinimumFileAge)

                        {

                            skippedCount++;

                            if (fileInfo.Exists)

                                remainingFiles.Add(file);

                            continue;

                        }

                        // Recheck that the file is not currently locked.

                        using (new FileStream(

                                   file.Path,

                                   FileMode.Open,

                                   FileAccess.Read,

                                   FileShare.None))

                        {

                        }

                        // Recheck its status immediately before deletion.

                        fileInfo.Refresh();

                        if (!fileInfo.Exists ||

                            (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0 ||

                            DateTime.Now - fileInfo.LastWriteTime < MinimumFileAge)

                        {

                            skippedCount++;

                            if (fileInfo.Exists)

                                remainingFiles.Add(file);

                            continue;

                        }

                        long sizeBeforeDeletion = fileInfo.Length;

                        File.Delete(file.Path);

                        if (!File.Exists(file.Path))

                        {

                            deletedCount++;

                            deletedBytes += sizeBeforeDeletion;

                        }

                        else

                        {

                            failedCount++;

                            remainingFiles.Add(file);

                        }

                    }

                    catch (UnauthorizedAccessException)

                    {

                        skippedCount++;

                        remainingFiles.Add(file);

                    }

                    catch (SecurityException)

                    {

                        skippedCount++;

                        remainingFiles.Add(file);

                    }

                    catch (IOException)

                    {

                        skippedCount++;

                        remainingFiles.Add(file);

                    }

                    catch (Exception)

                    {

                        failedCount++;

                        remainingFiles.Add(file);

                    }

                }

                files.Clear();

                files.AddRange(remainingFiles);

                resultsGrid.ItemsSource = CreateDisplayRows(files);

                string recycleBinResult = string.Empty;

                if (alsoEmptyRecycleBin == true)

                {

                    try

                    {

                        int result = SHEmptyRecycleBin(

                            window.IsVisible

                                ? new System.Windows.Interop.WindowInteropHelper(window).Handle

                                : IntPtr.Zero,

                            null,

                            RecycleBinNoConfirmation |

                            RecycleBinNoProgressUi |

                            RecycleBinNoSound);

                        recycleBinResult = result == 0

                            ? "\nRecycle Bin: emptied."

                            : $"\nRecycle Bin: operation failed (error {result}).";

                    }

                    catch (Exception ex)

                    {

                        recycleBinResult =

                            $"\nRecycle Bin: operation failed ({ex.Message}).";

                    }

                }

                summary.Text =

                    $"Cleanup finished.\n" +

                    $"Deleted: {deletedCount:N0} files ({FormatFileSize(deletedBytes)})\n" +

                    $"Skipped: {skippedCount:N0}\n" +

                    $"Failed: {failedCount:N0}" +

                    recycleBinResult +

                    "\n\nOnly eligible temporary files from the reviewed list were targeted.";

                CleanerStatusText.Text =

                    $"Cleanup finished. Deleted {deletedCount:N0} files; " +

                    $"skipped {skippedCount:N0}; failed {failedCount:N0}.";

                deleteButton.Content = "Delete Remaining Files";

                deleteButton.IsEnabled = files.Count > 0;

            }

            finally

            {

                closeButton.IsEnabled = true;

                ScanTemporaryFilesButton.IsEnabled = true;

            }

        };

        closeButton.Click += (_, _) => window.Close();

        buttonsPanel.Children.Add(deleteButton);

        buttonsPanel.Children.Add(closeButton);

        Grid.SetRow(buttonsPanel, 2);

        layout.Children.Add(buttonsPanel);

        window.Content = layout;

        window.ShowDialog();

    }

    private bool? ShowDeleteConfirmation(Window owner)

    {

        var dialog = new Window

        {

            Title = "Confirm Temporary File Cleanup",

            Width = 520,

            SizeToContent = SizeToContent.Height,

            MinHeight = 280,

            ResizeMode = ResizeMode.NoResize,

            WindowStartupLocation = WindowStartupLocation.CenterOwner,

            Owner = owner,

            Background = Brushes.White

        };

        var layout = new StackPanel

        {

            Margin = new Thickness(24)

        };

        layout.Children.Add(new TextBlock

        {

            Text = "Delete the eligible temporary files?",

            FontSize = 18,

            FontWeight = FontWeights.SemiBold,

            TextWrapping = TextWrapping.Wrap

        });

        layout.Children.Add(new TextBlock

        {

            Text =

                "The files in the scan results will be checked again before deletion. " +

                "Some files may be skipped if they are in use or no longer eligible.",

            Margin = new Thickness(0, 12, 0, 16),

            TextWrapping = TextWrapping.Wrap

        });

        var recycleBinCheckBox = new CheckBox

        {

            Content = "Also empty the Recycle Bin",

            IsChecked = false,

            FontWeight = FontWeights.SemiBold,

            Margin = new Thickness(0, 0, 0, 8)

        };

        layout.Children.Add(recycleBinCheckBox);

        var warning = new TextBlock

        {

            Text =

                "Warning: Emptying the Recycle Bin permanently deletes its contents. " +

                "You may not be able to recover those files.",

            TextWrapping = TextWrapping.Wrap,

            Foreground = Brushes.DarkRed,

            Margin = new Thickness(0, 0, 0, 18),

            Visibility = Visibility.Collapsed

        };

        layout.Children.Add(warning);

        recycleBinCheckBox.Checked += (_, _) =>

        {

            warning.Visibility = Visibility.Visible;

        };

        recycleBinCheckBox.Unchecked += (_, _) =>

        {

            warning.Visibility = Visibility.Collapsed;

        };

        var buttons = new StackPanel

        {

            Orientation = Orientation.Horizontal,

            HorizontalAlignment = HorizontalAlignment.Right

        };

        var cancelButton = new Button

        {

            Content = "Cancel",

            MinWidth = 95,

            Height = 36,

            Margin = new Thickness(0, 0, 10, 0),

            IsCancel = true

        };

        var confirmButton = new Button

        {

            Content = "Delete Files",

            MinWidth = 110,

            Height = 36,

            Background = new SolidColorBrush(Color.FromRgb(190, 45, 45)),

            Foreground = Brushes.White,

            BorderBrush = new SolidColorBrush(Color.FromRgb(190, 45, 45)),

            IsDefault = true

        };

        bool? result = null;

        cancelButton.Click += (_, _) =>

        {

            result = null;

            dialog.DialogResult = false;

        };

        confirmButton.Click += (_, _) =>

        {

            result = recycleBinCheckBox.IsChecked == true;

            dialog.DialogResult = true;

        };

        buttons.Children.Add(cancelButton);

        buttons.Children.Add(confirmButton);

        layout.Children.Add(buttons);

        dialog.Content = layout;

        bool? accepted = dialog.ShowDialog();

        return accepted == true ? result : null;

    }

    private static bool IsApprovedTemporaryFile(string filePath)

    {

        string fullPath = Path.GetFullPath(filePath);

        foreach (string root in GetTemporaryFolders())

        {

            string fullRoot = Path.GetFullPath(root)

                .TrimEnd(Path.DirectorySeparatorChar) +

                Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(

                    fullRoot,

                    StringComparison.OrdinalIgnoreCase))

            {

                return true;

            }

        }

        return false;

    }

    private static List<TemporaryFileDisplay> CreateDisplayRows(

        IEnumerable<TemporaryFileInfo> files)

    {

        return files.Select(file => new TemporaryFileDisplay

        {

            Path = file.Path,

            Size = FormatFileSize(file.SizeBytes),

            LastModified = file.LastModified.ToString("yyyy-MM-dd HH:mm:ss")

        }).ToList();

    }

    private static string FormatFileSize(long bytes)

    {

        string[] units = { "B", "KB", "MB", "GB", "TB" };

        double size = bytes;

        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)

        {

            size /= 1024;

            unitIndex++;

        }

        return $"{size:N2} {units[unitIndex]}";

    }

    private sealed class TemporaryFileInfo

    {

        public string Path { get; }

        public long SizeBytes { get; }

        public DateTime LastModified { get; }

        public TemporaryFileInfo(

            string path,

            long sizeBytes,

            DateTime lastModified)

        {

            Path = path;

            SizeBytes = sizeBytes;

            LastModified = lastModified;

        }

    }

    private sealed class TemporaryFileDisplay

    {

        public string Path { get; set; } = string.Empty;

        public string Size { get; set; } = string.Empty;

        public string LastModified { get; set; } = string.Empty;

    }

}
