namespace FolderAutomation.Core;

public class UndoResult
{
    public List<string> RestoredFiles { get; } = new();

    public List<string> SkippedFiles { get; } = new();

    public List<string> FailedFiles { get; } = new();
}