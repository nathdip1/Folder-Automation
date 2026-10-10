namespace FolderAutomation.Core;

public class UndoOperation
{
    public string OperationId { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;

    public string OriginalPath { get; set; } = string.Empty;

    public string NewPath { get; set; } = string.Empty;

    public DateTime ExecutedAt { get; set; }

    // Stores the backup path when an existing destination file
    // is replaced. Empty for normal move operations.
    public string ReplacedFileBackupPath { get; set; } = string.Empty;
}