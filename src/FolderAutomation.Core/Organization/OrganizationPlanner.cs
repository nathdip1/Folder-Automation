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
        string normalizedFolderPath = Path.GetFullPath(folderPath);

        return files
            .Select(file =>
            {
                var category = _categorizer.Categorize(file.FileType);

                return new OrganizationItem
                {
                    File = file,
                    Category = category,
                    DestinationPath = Path.Combine(
                        normalizedFolderPath,
                        category.ToString(),
                        file.Name)
                };
            })
            .ToList();
    }
}