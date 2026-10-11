namespace FolderAutomation.Core.FileRenamer;

public class RenameOperationResult
{
    public List<string> RenamedFiles { get; } = new();

    public List<string> SkippedFiles { get; } = new();

    public List<string> FailedFiles { get; } = new();

    public List<RenameHistoryEntry> RenameHistoryEntries { get; } = new();

    public bool HasChanges => RenamedFiles.Count > 0;
}