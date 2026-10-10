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
        IReadOnlyList<OrganizationItem> plan)
    {
        var result = new OrganizationResult();

        string operationId = Guid.NewGuid().ToString();
        string normalizedFolderPath = Path.GetFullPath(folderPath);

        foreach (var item in plan)
        {
            try
            {
                string sourcePath = Path.Combine(
                    normalizedFolderPath,
                    item.File.Name);

                string destinationPath = string.IsNullOrWhiteSpace(
                    item.DestinationPath)
                    ? Path.Combine(
                        normalizedFolderPath,
                        item.DestinationFolderName,
                        item.File.Name)
                    : Path.GetFullPath(item.DestinationPath);

                string destinationFolder =
                    Path.GetDirectoryName(destinationPath)
                    ?? throw new InvalidOperationException(
                        "The destination folder could not be determined.");

                if (!File.Exists(sourcePath))
                {
                    result.SkippedFiles.Add(
                        $"File no longer exists: {item.File.Name}");

                    continue;
                }

                if (File.Exists(destinationPath))
                {
                    result.SkippedFiles.Add(
                        $"Skipped because the destination file already exists: {item.File.Name}");

                    continue;
                }

                Directory.CreateDirectory(destinationFolder);

                File.Move(sourcePath, destinationPath);

                _undoRecorder.Record(new UndoOperation
                {
                    OperationId = operationId,
                    FolderPath = normalizedFolderPath,
                    OriginalPath = sourcePath,
                    NewPath = destinationPath,
                    ExecutedAt = DateTime.UtcNow
                });

                result.MovedFiles.Add(item.File.Name);
            }
            catch (Exception ex)
            {
                result.FailedFiles.Add(
                    $"{item.File.Name}: {ex.Message}");
            }
        }

        return result;
    }
}