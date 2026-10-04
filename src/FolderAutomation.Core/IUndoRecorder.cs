namespace FolderAutomation.Core;

public interface IUndoRecorder
{
    void Record(UndoOperation operation);
}