namespace FolderAutomation.Core;

public class OrganizationService
{
    private readonly IUndoRecorder _undoRecorder;

    public OrganizationService(IUndoRecorder undoRecorder)
    {
        _undoRecorder = undoRecorder;
    }

    public OrganizationResult Organize(
        string folderPath,
        IReadOnlyList<OrganizationItem> plan,
        FileConflictAction conflictAction = FileConflictAction.Skip,
        Func<OrganizationItem, string, FileConflictAction>? askHandler = null)
    {
        var result = new OrganizationResult();

        string operationId = Guid.NewGuid().ToString();
        string normalizedFolderPath = Path.GetFullPath(folderPath);

        foreach (var item in plan)
        {
            try
            {
                string sourcePath = Path.GetFullPath(
                    Path.Combine(normalizedFolderPath, item.File.Name));

                string destinationPath = string.IsNullOrWhiteSpace(item.DestinationPath)
                    ? Path.Combine(
                        normalizedFolderPath,
                        item.DestinationFolderName,
                        item.File.Name)
                    : Path.GetFullPath(item.DestinationPath);

                string destinationFolder = Path.GetDirectoryName(destinationPath)
                    ?? throw new InvalidOperationException(
                        "The destination folder could not be determined.");

                if (!File.Exists(sourcePath))
                {
                    result.SkippedFiles.Add(
                        $"File no longer exists: {item.File.Name}");
                    continue;
                }

                // Keep the action resolved for this specific file. In Ask mode,
                // the callback may return a different action for each conflict,
                // or remember an action for all remaining conflicts.
                FileConflictAction resolvedAction = conflictAction;

                if (File.Exists(destinationPath))
                {
                    if (conflictAction == FileConflictAction.Ask)
                    {
                        resolvedAction = askHandler?.Invoke(item, destinationPath)
                            ?? FileConflictAction.Skip;
                    }

                    if (resolvedAction == FileConflictAction.Cancel)
                    {
                        result.SkippedFiles.Add(
                            $"Operation cancelled before processing: {item.File.Name}");
                        break;
                    }

                    switch (resolvedAction)
                    {
                        case FileConflictAction.Ask:
                            result.SkippedFiles.Add(
                                $"No conflict resolution was selected: {item.File.Name}");
                            continue;

                        case FileConflictAction.Skip:
                            result.SkippedFiles.Add(
                                $"Skipped because the destination file already exists: {item.File.Name}");
                            continue;

                        case FileConflictAction.Rename:
                            destinationPath = GetUniqueDestinationPath(destinationPath);
                            destinationFolder = Path.GetDirectoryName(destinationPath)
                                ?? throw new InvalidOperationException(
                                    "The destination folder could not be determined.");
                            break;

                        case FileConflictAction.Replace:
                            break;

                        default:
                            result.FailedFiles.Add(
                                $"{item.File.Name}: Unsupported conflict action.");
                            continue;
                    }
                }

                Directory.CreateDirectory(destinationFolder);

                string backupPath = string.Empty;
                bool destinationBackedUp = false;
                bool incomingFileMoved = false;

                try
                {
                    // A destination may have appeared after the first check.
                    // Only Replace is allowed to overwrite that destination.
                    if (File.Exists(destinationPath))
                    {
                        if (resolvedAction != FileConflictAction.Replace)
                        {
                            result.SkippedFiles.Add(
                                $"Destination appeared during processing: {item.File.Name}");
                            continue;
                        }

                        backupPath = GetUniqueBackupPath(destinationFolder);
                        File.Move(destinationPath, backupPath);
                        destinationBackedUp = true;
                    }

                    File.Move(sourcePath, destinationPath);
                    incomingFileMoved = true;

                    _undoRecorder.Record(new UndoOperation
                    {
                        OperationId = operationId,
                        FolderPath = normalizedFolderPath,
                        OriginalPath = sourcePath,
                        NewPath = destinationPath,
                        ExecutedAt = DateTime.UtcNow,
                        ReplacedFileBackupPath = backupPath
                    });

                    result.MovedFiles.Add(item.File.Name);
                }
                catch
                {
                    // Roll back filesystem changes if the move or its Undo
                    // record could not be completed.
                    if (incomingFileMoved &&
                        File.Exists(destinationPath) &&
                        !File.Exists(sourcePath))
                    {
                        File.Move(destinationPath, sourcePath);
                    }

                    if (destinationBackedUp &&
                        File.Exists(backupPath) &&
                        !File.Exists(destinationPath))
                    {
                        File.Move(backupPath, destinationPath);
                    }

                    throw;
                }
            }
            catch (Exception ex)
            {
                result.FailedFiles.Add($"{item.File.Name}: {ex.Message}");
            }
        }

        return result;
    }

    private static string GetUniqueDestinationPath(string destinationPath)
    {
        string directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException(
                "The destination folder could not be determined.");

        string fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(destinationPath);

        string extension = Path.GetExtension(destinationPath);

        int counter = 1;
        string candidatePath;

        do
        {
            candidatePath = Path.Combine(
                directory,
                $"{fileNameWithoutExtension} ({counter}){extension}");
            counter++;
        }
        while (File.Exists(candidatePath) || Directory.Exists(candidatePath));

        return candidatePath;
    }

    private static string GetUniqueBackupPath(string destinationFolder)
    {
        string backupPath;

        do
        {
            backupPath = Path.Combine(
                destinationFolder,
                $".folderautomation-backup-{Guid.NewGuid():N}.bak");
        }
        while (File.Exists(backupPath) || Directory.Exists(backupPath));

        return backupPath;
    }
}
