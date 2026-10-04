namespace FolderAutomation.Core;

public class OrganizationPlanner
{
    private readonly FileCategorizer _categorizer;

    public OrganizationPlanner()
    {
        _categorizer = new FileCategorizer();
    }

    public IReadOnlyList<OrganizationItem> CreatePlan(
        string folderPath,
        IReadOnlyList<FileItem> files)
    {
        return files
            .Select(file => new OrganizationItem
            {
                File = file,
                Category = _categorizer.Categorize(file.FileType)
            })
            .ToList();
    }
}