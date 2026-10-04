namespace FolderAutomation.Controls.FolderBrowser;

public class FolderNode
{
    public string Name { get; }

    public string FullPath { get; }

    public FolderNode(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
    }
}