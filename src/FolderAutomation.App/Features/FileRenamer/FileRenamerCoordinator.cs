using FolderAutomation.Core;
using FolderAutomation.Core.FileRenamer;
using FolderAutomation.Data;
using FolderAutomation.Data.FileRenamer;

namespace FolderAutomation.Features.FileRenamer;

public class FileRenamerCoordinator
{
    private readonly RenameFileService _fileService;
    private readonly RenamePlanner _planner;
    private readonly RenameExecutionService _executionService;
    private readonly RenameUndoService _undoService;
    private readonly RenameHistoryRepository _historyRepository;

    public FileRenamerCoordinator(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var databaseConnection = new DatabaseConnection(databasePath);

        _fileService = new RenameFileService();
        _planner = new RenamePlanner();
        _executionService = new RenameExecutionService();
        _undoService = new RenameUndoService();
        _historyRepository = new RenameHistoryRepository(
            databaseConnection);

        _historyRepository.CreateTable();
    }

    public IReadOnlyList<RenameFileItem> LoadFiles(string folderPath)
    {
        return _fileService.GetFiles(folderPath);
    }

    public IReadOnlyList<RenamePreviewItem> CreatePreview(
        IReadOnlyList<RenameFileItem> selectedFiles,
        IReadOnlyDictionary<RenameFileCategory, string> baseNames)
    {
        return _planner.CreatePreview(selectedFiles, baseNames);
    }

    public RenameOperationResult ExecuteRename(
        IReadOnlyList<RenamePreviewItem> previewItems)
    {
        RenameOperationResult result =
            _executionService.Execute(previewItems);

        if (!result.HasChanges ||
            result.FailedFiles.Count > 0 ||
            result.RenameHistoryEntries.Count == 0)
        {
            return result;
        }

        try
        {
            _historyRepository.RecordOperation(
                result.RenameHistoryEntries);
        }
        catch (Exception ex)
        {
            result.FailedFiles.Add(
                "Files were renamed, but their undo history could not be saved to SQLite. " +
                "Do not clear the application's database. " +
                $"Database error: {ex.Message}");
        }

        return result;
    }

    public List<RenameHistoryEntry> GetLatestOperation(string folderPath)
    {
        return _historyRepository.GetLatestOperation(folderPath);
    }

    public List<RenameHistoryEntry> GetOperations(string folderPath)
    {
        return _historyRepository.GetOperations(folderPath);
    }

    public UndoResult UndoLatestOperation(string folderPath)
    {
        List<RenameHistoryEntry> historyEntries =
            _historyRepository.GetLatestOperation(folderPath);

        if (historyEntries.Count == 0)
        {
            var emptyResult = new UndoResult();

            emptyResult.FailedFiles.Add(
                "No saved, undoable rename history exists for this folder. " +
                "No files were changed.");

            return emptyResult;
        }

        UndoResult result = _undoService.Undo(historyEntries);

        if (result.FailedFiles.Count == 0 &&
            result.SkippedFiles.Count == 0 &&
            result.RestoredFiles.Count == historyEntries.Count)
        {
            _historyRepository.MarkOperationUndone(
                historyEntries[0].OperationId,
                historyEntries[0].FolderPath);
        }

        return result;
    }
}