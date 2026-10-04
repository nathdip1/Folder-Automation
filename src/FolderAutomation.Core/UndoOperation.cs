namespace FolderAutomation.Core;

public class UndoOperation
{
    public string OperationId { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;

    public string OriginalPath { get; set; } = string.Empty;

    public string NewPath { get; set; } = string.Empty;

    public DateTime ExecutedAt { get; set; }
}