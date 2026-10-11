using FolderAutomation.Core;
using System.IO;

namespace FolderAutomation.Core.FileRenamer;

public class RenameUndoService
{
    public UndoResult Undo(
        IReadOnlyList<RenameHistoryEntry> historyEntries)
    {
        ArgumentNullException.ThrowIfNull(historyEntries);

        var result = new UndoResult();

        if (historyEntries.Count == 0)
        {
            result.FailedFiles.Add(
                "No rename history was provided. No files were changed.");

            return result;
        }

        var operationIds = historyEntries
            .Select(entry => entry.OperationId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (operationIds.Count != 1)
        {
            result.FailedFiles.Add(
                "The history contains multiple operations. Undo one operation at a time.");

            return result;
        }

        var entries = historyEntries.ToList();

        var sourcePaths = new HashSet<string>(
            entries.Select(entry => Path.GetFullPath(entry.NewPath)),
            StringComparer.OrdinalIgnoreCase);

        var originalPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (RenameHistoryEntry entry in entries)
        {
            string newPath = Path.GetFullPath(entry.NewPath);
            string originalPath = Path.GetFullPath(entry.OriginalPath);

            if (!originalPaths.Add(originalPath))
            {
                result.FailedFiles.Add(
                    $"Duplicate original destination detected: {originalPath}");

                return result;
            }

            if (!File.Exists(newPath))
            {
                result.FailedFiles.Add(
                    $"The renamed file is missing: {newPath}");

                return result;
            }

            if (Directory.Exists(originalPath))
            {
                result.FailedFiles.Add(
                    $"A directory occupies the original path: {originalPath}");

                return result;
            }

            if (File.Exists(originalPath) &&
                !sourcePaths.Contains(originalPath))
            {
                result.FailedFiles.Add(
                    $"The original path is occupied by another file: {originalPath}");

                return result;
            }
        }

        var stagedFiles = new List<StagedUndo>();

        try
        {
            // Stage all renamed files under unique temporary names.
            foreach (RenameHistoryEntry entry in entries)
            {
                string currentPath = Path.GetFullPath(entry.NewPath);
                string directory = Path.GetDirectoryName(currentPath)!;
                string temporaryPath;

                do
                {
                    temporaryPath = Path.Combine(
                        directory,
                        $".folderautomation-undo-{Guid.NewGuid():N}.tmp");
                }
                while (File.Exists(temporaryPath) ||
                       Directory.Exists(temporaryPath));

                File.Move(currentPath, temporaryPath);

                stagedFiles.Add(
                    new StagedUndo(
                        currentPath,
                        temporaryPath,
                        Path.GetFullPath(entry.OriginalPath)));
            }

            // Restore the files to their original names.
            foreach (StagedUndo stagedFile in stagedFiles)
            {
                if (File.Exists(stagedFile.OriginalPath) ||
                    Directory.Exists(stagedFile.OriginalPath))
                {
                    throw new IOException(
                        $"The original destination became occupied: {stagedFile.OriginalPath}");
                }

                File.Move(
                    stagedFile.TemporaryPath,
                    stagedFile.OriginalPath);

                stagedFile.IsRestored = true;
            }

            foreach (StagedUndo stagedFile in stagedFiles)
            {
                result.RestoredFiles.Add(stagedFile.OriginalPath);
            }
        }
        catch (Exception ex)
        {
            result.FailedFiles.Add(
                $"Undo encountered an error: {ex.Message}");

            // Best-effort rollback to the renamed paths.
            for (int index = stagedFiles.Count - 1; index >= 0; index--)
            {
                StagedUndo stagedFile = stagedFiles[index];

                string currentPath = stagedFile.IsRestored
                    ? stagedFile.OriginalPath
                    : stagedFile.TemporaryPath;

                if (!File.Exists(currentPath))
                {
                    continue;
                }

                if (File.Exists(stagedFile.NewPath) ||
                    Directory.Exists(stagedFile.NewPath))
                {
                    result.FailedFiles.Add(
                        $"Rollback could not restore the renamed path because it is occupied: {stagedFile.NewPath}");

                    continue;
                }

                try
                {
                    File.Move(currentPath, stagedFile.NewPath);
                }
                catch (Exception rollbackException)
                {
                    result.FailedFiles.Add(
                        $"Rollback failed for {currentPath}: {rollbackException.Message}");
                }
            }

            // Do not report a failed or partially rolled-back operation as successful.
            result.RestoredFiles.Clear();
        }

        return result;
    }

    private sealed class StagedUndo
    {
        public string NewPath { get; }

        public string TemporaryPath { get; }

        public string OriginalPath { get; }

        public bool IsRestored { get; set; }

        public StagedUndo(
            string newPath,
            string temporaryPath,
            string originalPath)
        {
            NewPath = newPath;
            TemporaryPath = temporaryPath;
            OriginalPath = originalPath;
        }
    }
}