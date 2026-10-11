using FolderAutomation.Core.FileRenamer;
using Microsoft.Data.Sqlite;

namespace FolderAutomation.Data.FileRenamer;

public class RenameHistoryRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public RenameHistoryRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public void CreateTable()
    {
        using var connection = _databaseConnection.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS RenameHistory
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OperationId TEXT NOT NULL,
                FolderPath TEXT NOT NULL,
                OriginalPath TEXT NOT NULL,
                NewPath TEXT NOT NULL,
                ExecutedAt TEXT NOT NULL,
                IsUndone INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS IX_RenameHistory_FolderPath
            ON RenameHistory(FolderPath);

            CREATE INDEX IF NOT EXISTS IX_RenameHistory_OperationId
            ON RenameHistory(OperationId);
            """;

        command.ExecuteNonQuery();
    }

    public void Record(RenameHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        RecordOperation([entry]);
    }

    public void RecordOperation(
        IReadOnlyList<RenameHistoryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return;
        }

        string operationId = entries[0].OperationId;
        string folderPath = entries[0].FolderPath;

        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException(
                "The operation ID cannot be empty.",
                nameof(entries));
        }

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException(
                "The folder path cannot be empty.",
                nameof(entries));
        }

        if (entries.Any(entry =>
                entry is null ||
                !string.Equals(
                    entry.OperationId,
                    operationId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entry.FolderPath,
                    folderPath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "All history entries must belong to the same operation and folder.",
                nameof(entries));
        }

        using var connection = _databaseConnection.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (RenameHistoryEntry entry in entries)
            {
                using var command = connection.CreateCommand();

                command.Transaction = transaction;

                command.CommandText = """
                    INSERT INTO RenameHistory
                    (
                        OperationId,
                        FolderPath,
                        OriginalPath,
                        NewPath,
                        ExecutedAt,
                        IsUndone
                    )
                    VALUES
                    (
                        $operationId,
                        $folderPath,
                        $originalPath,
                        $newPath,
                        $executedAt,
                        0
                    );
                    """;

                command.Parameters.AddWithValue(
                    "$operationId",
                    entry.OperationId);

                command.Parameters.AddWithValue(
                    "$folderPath",
                    entry.FolderPath);

                command.Parameters.AddWithValue(
                    "$originalPath",
                    entry.OriginalPath);

                command.Parameters.AddWithValue(
                    "$newPath",
                    entry.NewPath);

                command.Parameters.AddWithValue(
                    "$executedAt",
                    entry.ExecutedAt.ToString("O"));

                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<RenameHistoryEntry> GetLatestOperation(string folderPath)
    {
        using var connection = _databaseConnection.CreateConnection();
        connection.Open();

        string? operationId;

        using (var operationCommand = connection.CreateCommand())
        {
            operationCommand.CommandText = """
                SELECT OperationId
                FROM RenameHistory
                WHERE FolderPath = $folderPath
                  AND IsUndone = 0
                ORDER BY Id DESC
                LIMIT 1;
                """;

            operationCommand.Parameters.AddWithValue("$folderPath", folderPath);

            operationId = operationCommand.ExecuteScalar()?.ToString();
        }

        if (string.IsNullOrWhiteSpace(operationId))
        {
            return [];
        }

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT OperationId, FolderPath, OriginalPath, NewPath, ExecutedAt
            FROM RenameHistory
            WHERE OperationId = $operationId
              AND FolderPath = $folderPath
              AND IsUndone = 0
            ORDER BY Id DESC;
            """;

        command.Parameters.AddWithValue("$operationId", operationId);
        command.Parameters.AddWithValue("$folderPath", folderPath);

        using var reader = command.ExecuteReader();

        var entries = new List<RenameHistoryEntry>();

        while (reader.Read())
        {
            entries.Add(new RenameHistoryEntry
            {
                OperationId = reader.GetString(0),
                FolderPath = reader.GetString(1),
                OriginalPath = reader.GetString(2),
                NewPath = reader.GetString(3),
                ExecutedAt = DateTime.Parse(reader.GetString(4))
            });
        }

        return entries;
    }

    public List<RenameHistoryEntry> GetOperations(string folderPath)
    {
        using var connection = _databaseConnection.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT OperationId, FolderPath, OriginalPath, NewPath, ExecutedAt
            FROM RenameHistory
            WHERE FolderPath = $folderPath
              AND IsUndone = 0
            ORDER BY Id DESC;
            """;

        command.Parameters.AddWithValue("$folderPath", folderPath);

        using var reader = command.ExecuteReader();

        var entries = new List<RenameHistoryEntry>();

        while (reader.Read())
        {
            entries.Add(new RenameHistoryEntry
            {
                OperationId = reader.GetString(0),
                FolderPath = reader.GetString(1),
                OriginalPath = reader.GetString(2),
                NewPath = reader.GetString(3),
                ExecutedAt = DateTime.Parse(reader.GetString(4))
            });
        }

        return entries;
    }

    public void MarkOperationUndone(string operationId, string folderPath)
    {
        using var connection = _databaseConnection.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE RenameHistory
            SET IsUndone = 1
            WHERE OperationId = $operationId
              AND FolderPath = $folderPath
              AND IsUndone = 0;
            """;

        command.Parameters.AddWithValue("$operationId", operationId);
        command.Parameters.AddWithValue("$folderPath", folderPath);

        command.ExecuteNonQuery();
    }
}