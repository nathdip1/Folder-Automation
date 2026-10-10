namespace FolderAutomation.Core;

public class UndoService
{
    public UndoResult Undo(IReadOnlyList<UndoOperation> operations)
    {
        var result = new UndoResult();

        foreach (var operation in operations)
        {
            try
            {
                if (!File.Exists(operation.NewPath))
                {
                    result.SkippedFiles.Add(
                        $"File no longer exists: {operation.NewPath}");
                    continue;
                }

                if (File.Exists(operation.OriginalPath))
                {
                    result.SkippedFiles.Add(
                        $"Original file already exists: {operation.OriginalPath}");
                    continue;
                }

                bool hasReplacementBackup =
                    !string.IsNullOrWhiteSpace(
                        operation.ReplacedFileBackupPath);

                if (hasReplacementBackup &&
                    !File.Exists(operation.ReplacedFileBackupPath))
                {
                    result.FailedFiles.Add(
                        $"Cannot undo replacement because the backup is missing: " +
                        operation.ReplacedFileBackupPath);
                    continue;
                }

                string? originalDirectory =
                    Path.GetDirectoryName(operation.OriginalPath);

                if (!string.IsNullOrWhiteSpace(originalDirectory))
                {
                    Directory.CreateDirectory(originalDirectory);
                }

                // Move the organized file back to its original location.
                File.Move(
                    operation.NewPath,
                    operation.OriginalPath);

                try
                {
                    if (hasReplacementBackup)
                    {
                        // Restore the file that was replaced.
                        File.Move(
                            operation.ReplacedFileBackupPath,
                            operation.NewPath);
                    }

                    result.RestoredFiles.Add(
                        operation.OriginalPath);
                }
                catch
                {
                    // If restoring the replacement backup fails,
                    // attempt to return the incoming file to its
                    // organized location to avoid a partial undo.
                    if (File.Exists(operation.OriginalPath) &&
                        !File.Exists(operation.NewPath))
                    {
                        File.Move(
                            operation.OriginalPath,
                            operation.NewPath);
                    }

                    throw;
                }
            }
            catch (Exception ex)
            {
                result.FailedFiles.Add(
                    $"{operation.NewPath}: {ex.Message}");
            }
        }

        return result;
    }
}
