namespace FolderAutomation.Core;

public class OrganizationResult
{
    public List<string> MovedFiles { get; } = new();

    public List<string> SkippedFiles { get; } = new();

    public List<string> FailedFiles { get; } = new();
}