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

                string? originalDirectory =
                    Path.GetDirectoryName(operation.OriginalPath);

                if (!string.IsNullOrWhiteSpace(originalDirectory))
                {
                    Directory.CreateDirectory(originalDirectory);
                }

                File.Move(
                    operation.NewPath,
                    operation.OriginalPath);

                result.RestoredFiles.Add(
                    operation.OriginalPath);
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