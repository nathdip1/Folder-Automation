using System.IO;

namespace FolderAutomation.Core.FileRenamer;

public class RenameExecutionService
{
    public RenameOperationResult Execute(
        IReadOnlyList<RenamePreviewItem> previewItems)
    {
        ArgumentNullException.ThrowIfNull(previewItems);

        var result = new RenameOperationResult();

        if (previewItems.Count == 0)
        {
            return result;
        }

        if (previewItems.Any(item => !item.IsValid))
        {
            result.FailedFiles.Add(
                "The rename operation was not started because the preview contains invalid items.");

            return result;
        }

        var sourcePaths = new HashSet<string>(
            previewItems.Select(item => Path.GetFullPath(item.OriginalPath)),
            StringComparer.OrdinalIgnoreCase);

        var targetPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (RenamePreviewItem item in previewItems)
        {
            if (!File.Exists(item.OriginalPath))
            {
                result.SkippedFiles.Add(
                    $"Source file no longer exists: {item.OriginalPath}");

                continue;
            }

            string targetPath = Path.GetFullPath(item.ProposedPath);

            if (!targetPaths.Add(targetPath))
            {
                result.FailedFiles.Add(
                    $"Duplicate destination detected: {targetPath}");

                return result;
            }

            if (Directory.Exists(targetPath))
            {
                result.FailedFiles.Add(
                    $"A directory already exists at the destination: {targetPath}");

                return result;
            }

            if (File.Exists(targetPath) &&
                !sourcePaths.Contains(targetPath))
            {
                result.FailedFiles.Add(
                    $"A file already exists at the destination: {targetPath}");

                return result;
            }
        }

        if (result.SkippedFiles.Count > 0)
        {
            result.FailedFiles.Add(
                "The rename operation was not started because one or more source files are missing.");

            return result;
        }

        var stagedFiles = new List<StagedRename>();

        try
        {
            // Stage all source files under unique temporary names.
            foreach (RenamePreviewItem item in previewItems)
            {
                string originalPath = Path.GetFullPath(item.OriginalPath);

                string directory = Path.GetDirectoryName(originalPath)!;

                string temporaryPath;

                do
                {
                    temporaryPath = Path.Combine(
                        directory,
                        $".folderautomation-rename-{Guid.NewGuid():N}.tmp");
                }
                while (File.Exists(temporaryPath) ||
                       Directory.Exists(temporaryPath));

                File.Move(originalPath, temporaryPath);

                stagedFiles.Add(
                    new StagedRename(
                        item,
                        originalPath,
                        temporaryPath,
                        Path.GetFullPath(item.ProposedPath)));
            }

            // Move staged files to their final destinations.
            foreach (StagedRename stagedFile in stagedFiles)
            {
                if (File.Exists(stagedFile.ProposedPath) ||
                    Directory.Exists(stagedFile.ProposedPath))
                {
                    throw new IOException(
                        $"The destination became occupied during renaming: {stagedFile.ProposedPath}");
                }

                File.Move(
                    stagedFile.TemporaryPath,
                    stagedFile.ProposedPath);

                stagedFile.IsCompleted = true;
            }

            // Record successful renames only after the whole operation succeeds.
            DateTime executedAt = DateTime.UtcNow;

            foreach (StagedRename stagedFile in stagedFiles)
            {
                result.RenamedFiles.Add(stagedFile.ProposedPath);

                result.RenameHistoryEntries.Add(
                    new RenameHistoryEntry
                    {
                        OperationId = stagedFile.PreviewItem.OperationId,
                        FolderPath = Path.GetDirectoryName(
                            stagedFile.OriginalPath)!,
                        OriginalPath = stagedFile.OriginalPath,
                        NewPath = stagedFile.ProposedPath,
                        ExecutedAt = executedAt
                    });
            }
        }
        catch (Exception ex)
        {
            result.FailedFiles.Add(
                $"The rename operation encountered an error: {ex.Message}");

            // A failed operation must not be presented as a successful operation.
            result.RenamedFiles.Clear();
            result.RenameHistoryEntries.Clear();

            // Best-effort rollback. Never overwrite an existing file.
            for (int index = stagedFiles.Count - 1; index >= 0; index--)
            {
                StagedRename stagedFile = stagedFiles[index];

                string currentPath = stagedFile.IsCompleted
                    ? stagedFile.ProposedPath
                    : stagedFile.TemporaryPath;

                if (!File.Exists(currentPath))
                {
                    continue;
                }

                if (File.Exists(stagedFile.OriginalPath) ||
                    Directory.Exists(stagedFile.OriginalPath))
                {
                    result.FailedFiles.Add(
                        $"Rollback could not restore the original name because the destination is occupied: {stagedFile.OriginalPath}");

                    continue;
                }

                try
                {
                    File.Move(currentPath, stagedFile.OriginalPath);
                }
                catch (Exception rollbackException)
                {
                    result.FailedFiles.Add(
                        $"Rollback failed for {currentPath}: {rollbackException.Message}");
                }
            }
        }

        return result;
    }

    private sealed class StagedRename
    {
        public RenamePreviewItem PreviewItem { get; }

        public string OriginalPath { get; }

        public string TemporaryPath { get; }

        public string ProposedPath { get; }

        public bool IsCompleted { get; set; }

        public StagedRename(
            RenamePreviewItem previewItem,
            string originalPath,
            string temporaryPath,
            string proposedPath)
        {
            PreviewItem = previewItem;
            OriginalPath = originalPath;
            TemporaryPath = temporaryPath;
            ProposedPath = proposedPath;
        }
    }
}