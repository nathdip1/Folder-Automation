namespace FolderAutomation.Core;

public class FileItem
{
    public string Name { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public long SizeInBytes { get; set; }

    public DateTime Modified { get; set; }
}