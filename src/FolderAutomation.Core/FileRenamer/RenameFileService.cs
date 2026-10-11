using System.IO;

namespace FolderAutomation.Core.FileRenamer;

public class RenameFileService
{
    private readonly RenameFileCategorizer _categorizer;

    public RenameFileService()
    {
        _categorizer = new RenameFileCategorizer();
    }

    public IReadOnlyList<RenameFileItem> GetFiles(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) ||
            !Directory.Exists(folderPath))
        {
            return [];
        }

        return Directory
            .GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Select(filePath =>
            {
                var fileInfo = new FileInfo(filePath);

                return new RenameFileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    Extension = fileInfo.Extension,
                    Category = _categorizer.Categorize(fileInfo.Extension)
                };
            })
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
