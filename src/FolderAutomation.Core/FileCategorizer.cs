namespace FolderAutomation.Core;

public class FileCategorizer
{
    public FileCategory Categorize(string fileExtension)
    {
        string extension = fileExtension.ToLowerInvariant();

        return extension switch
        {
            // Pictures
            ".jpg" or
            ".jpeg" or
            ".png" or
            ".gif" or
            ".webp" or
            ".bmp" or
            ".tiff" or
            ".tif" or
            ".svg" or
            ".ico"
                => FileCategory.Pictures,

            // Videos
            ".mp4" or
            ".mkv" or
            ".avi" or
            ".mov" or
            ".wmv" or
            ".webm" or
            ".flv" or
            ".m4v"
                => FileCategory.Videos,

            // Audio
            ".mp3" or
            ".wav" or
            ".flac" or
            ".aac" or
            ".m4a" or
            ".ogg" or
            ".wma"
                => FileCategory.Audio,

            // Documents
            ".pdf" or
            ".doc" or
            ".docx" or
            ".txt" or
            ".rtf" or
            ".odt" or
            ".ppt" or
            ".pptx" or
            ".odp"
                => FileCategory.Documents,

            // Spreadsheets
            ".xls" or
            ".xlsx" or
            ".xlsm" or
            ".csv" or
            ".ods"
                => FileCategory.Spreadsheets,

            // Archives
            ".zip" or
            ".rar" or
            ".7z" or
            ".tar" or
            ".gz" or
            ".bz2"
                => FileCategory.Archives,

            // Anything unknown
            _ => FileCategory.Other
        };
    }
}