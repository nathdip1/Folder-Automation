namespace FolderAutomation.Core.FileRenamer;

public class RenameFileItem
{
    public string Name { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public RenameFileCategory Category { get; set; }

    public bool IsSelected { get; set; }

    public string ProposedName { get; set; } = string.Empty;

    public string ProposedPath { get; set; } = string.Empty;
}
