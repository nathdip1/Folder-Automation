using System.IO;

namespace FolderAutomation.Core;

public class FileService
{
    public IReadOnlyList<FileItem> GetFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        return Directory
            .GetFiles(folderPath)
            .Select(filePath =>
            {
                var fileInfo = new FileInfo(filePath);

                return new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    FileType = fileInfo.Extension,
                    SizeInBytes = fileInfo.Length,
                    Modified = fileInfo.LastWriteTime
                };
            })
            .ToList();
    }
}