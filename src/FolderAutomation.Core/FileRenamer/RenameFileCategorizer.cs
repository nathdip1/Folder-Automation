namespace FolderAutomation.Core.FileRenamer;

public class RenameFileCategorizer
{
    public RenameFileCategory Categorize(string fileExtension)
    {
        string extension = fileExtension.ToLowerInvariant();

        return extension switch
        {
            // Photos
            ".jpg" or
            ".jpeg" or
            ".png" or
            ".gif" or
            ".webp" or
            ".bmp" or
            ".tiff" or
            ".tif" or
            ".svg" or
            ".ico" or
            ".heic" or
            ".heif"
                => RenameFileCategory.Photos,

            // Videos
            ".mp4" or
            ".mkv" or
            ".avi" or
            ".mov" or
            ".wmv" or
            ".webm" or
            ".flv" or
            ".m4v" or
            ".mpeg" or
            ".mpg"
                => RenameFileCategory.Videos,

            // PDF files
            ".pdf"
                => RenameFileCategory.Pdfs,

            // Word documents
            ".doc" or
            ".docx"
                => RenameFileCategory.WordDocuments,

            // Music and audio
            ".mp3" or
            ".wav" or
            ".flac" or
            ".aac" or
            ".m4a" or
            ".ogg" or
            ".wma" or
            ".aiff" or
            ".alac" or
            ".opus"
                => RenameFileCategory.Music,

            // Excel spreadsheets
            ".xls" or
            ".xlsx" or
            ".xlsm"
                => RenameFileCategory.Excel,

            // Remaining file types
            _ => RenameFileCategory.Other
        };
    }
}
