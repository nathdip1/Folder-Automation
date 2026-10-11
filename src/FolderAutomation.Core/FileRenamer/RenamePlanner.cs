using System.IO;

namespace FolderAutomation.Core.FileRenamer;

public class RenamePlanner
{
    public IReadOnlyList<RenamePreviewItem> CreatePreview(
        IReadOnlyList<RenameFileItem> selectedFiles,
        IReadOnlyDictionary<RenameFileCategory, string> baseNames)
    {
        ArgumentNullException.ThrowIfNull(selectedFiles);
        ArgumentNullException.ThrowIfNull(baseNames);

        string operationId = Guid.NewGuid().ToString("N");

        var preview = new List<RenamePreviewItem>();
        var reservedTargets = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var selectedSourcePaths = new HashSet<string>(
            selectedFiles.Select(file => Path.GetFullPath(file.FullPath)),
            StringComparer.OrdinalIgnoreCase);

        foreach (RenameFileCategory category in Enum.GetValues<RenameFileCategory>())
        {
            List<RenameFileItem> categoryFiles = selectedFiles
                .Where(file => file.Category == category)
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (categoryFiles.Count == 0)
            {
                continue;
            }

            if (!baseNames.TryGetValue(category, out string? baseName) ||
                string.IsNullOrWhiteSpace(baseName))
            {
                foreach (RenameFileItem file in categoryFiles)
                {
                    preview.Add(CreateInvalidItem(
                        operationId,
                        file,
                        "A base name is required for this category."));
                }

                continue;
            }

            baseName = baseName.Trim();

            if (baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                baseName is "." or ".." ||
                baseName.EndsWith(' ') ||
                baseName.EndsWith('.'))
            {
                foreach (RenameFileItem file in categoryFiles)
                {
                    preview.Add(CreateInvalidItem(
                        operationId,
                        file,
                        "The base name contains invalid filename characters or ends with a space or period."));
                }

                continue;
            }

            for (int index = 0; index < categoryFiles.Count; index++)
            {
                RenameFileItem file = categoryFiles[index];
                string extension = Path.GetExtension(file.FullPath);

                string newName = index == 0
                    ? $"{baseName}{extension}"
                    : $"{baseName}({index}){extension}";

                string proposedPath = Path.Combine(
                    Path.GetDirectoryName(file.FullPath)!,
                    newName);

                string validationMessage = string.Empty;
                bool isValid = true;

                if (!reservedTargets.Add(proposedPath))
                {
                    isValid = false;
                    validationMessage = "Another selected file has the same proposed destination.";
                }
                else if (
                    File.Exists(proposedPath) &&
                    !selectedSourcePaths.Contains(proposedPath))
                {
                    isValid = false;
                    validationMessage = "A file already exists at the proposed destination.";
                }
                else if (
                    Directory.Exists(proposedPath))
                {
                    isValid = false;
                    validationMessage = "A directory already exists at the proposed destination.";
                }

                preview.Add(new RenamePreviewItem
                {
                    OperationId = operationId,
                    OriginalPath = file.FullPath,
                    ProposedPath = proposedPath,
                    Category = category,
                    IsValid = isValid,
                    ValidationMessage = validationMessage
                });
            }
        }

        return preview
            .OrderBy(item => item.Category)
            .ThenBy(item => item.OriginalPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RenamePreviewItem CreateInvalidItem(
        string operationId,
        RenameFileItem file,
        string message)
    {
        return new RenamePreviewItem
        {
            OperationId = operationId,
            OriginalPath = file.FullPath,
            ProposedPath = string.Empty,
            Category = file.Category,
            IsValid = false,
            ValidationMessage = message
        };
    }
}
