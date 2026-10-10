namespace FolderAutomation.Core;

public class OrganizationItem
{
    public FileItem File { get; set; } = null!;

    public FileCategory Category { get; set; }

    public string DestinationFolderName => Category.ToString();

    public string DestinationPath { get; set; } = string.Empty;
}