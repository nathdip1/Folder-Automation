namespace FolderAutomation.Core.FileRenamer;

public class RenamePreviewItem
{
    public string OperationId { get; set; } = string.Empty;

    public string OriginalPath { get; set; } = string.Empty;

    public string ProposedPath { get; set; } = string.Empty;

    public RenameFileCategory Category { get; set; }

    public bool IsValid { get; set; }

    public string ValidationMessage { get; set; } = string.Empty;
}
