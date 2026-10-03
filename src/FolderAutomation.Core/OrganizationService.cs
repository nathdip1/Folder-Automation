namespace FolderAutomation.Core;

public class OrganizationService
{
    public OrganizationResult Organize(
        string folderPath,
        IReadOnlyList<OrganizationItem> plan)
    {
        var result = new OrganizationResult();

        foreach (var item in plan)
        {
            try
            {
                string sourcePath = Path.Combine(
                    folderPath,
                    item.File.Name);

                string destinationFolder = Path.Combine(
                    folderPath,
                    item.DestinationFolderName);

                Directory.CreateDirectory(destinationFolder);

                string destinationPath = Path.Combine(
                    destinationFolder,
                    item.File.Name);

                if (File.Exists(destinationPath))
                {
                    result.SkippedFiles.Add(
                        $"Skipped because the file already exists: {item.File.Name}");

                    continue;
                }

                if (!File.Exists(sourcePath))
                {
                    result.SkippedFiles.Add(
                        $"File no longer exists: {item.File.Name}");

                    continue;
                }

                File.Move(sourcePath, destinationPath);

                result.MovedFiles.Add(item.File.Name);
            }
            catch (Exception ex)
            {
                result.FailedFiles.Add(
                    $"{item.File.Name}: {ex.Message}");
            }
        }

        return result;
    }
}