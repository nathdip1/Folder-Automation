using Microsoft.Data.Sqlite;
using FolderAutomation.Core;

namespace FolderAutomation.Data;

public class UndoRepository : IUndoRecorder
{
    private readonly DatabaseConnection _databaseConnection;

    public UndoRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public void CreateTable()
    {
        using var connection = _databaseConnection.CreateConnection();

        connection.Open();

        using var createTableCommand = connection.CreateCommand();

        createTableCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS UndoOperations
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OperationId TEXT NOT NULL,
                FolderPath TEXT NOT NULL,
                OriginalPath TEXT NOT NULL,
                NewPath TEXT NOT NULL,
                ExecutedAt TEXT NOT NULL,
                ReplacedFileBackupPath TEXT NOT NULL DEFAULT ''
            );
            """;

        createTableCommand.ExecuteNonQuery();

        AddColumnIfNeeded(
            connection,
            "OperationId",
            "TEXT NOT NULL DEFAULT ''");

        AddColumnIfNeeded(
            connection,
            "FolderPath",
            "TEXT NOT NULL DEFAULT ''");

        AddColumnIfNeeded(
            connection,
            "ReplacedFileBackupPath",
            "TEXT NOT NULL DEFAULT ''");
    }

    private static void AddColumnIfNeeded(
        SqliteConnection connection,
        string columnName,
        string columnDefinition)
    {
        using var command = connection.CreateCommand();

        command.CommandText = """
            PRAGMA table_info(UndoOperations);
            """;

        using var reader = command.ExecuteReader();

        bool columnExists = false;

        while (reader.Read())
        {
            string existingColumnName = reader.GetString(1);

            if (existingColumnName.Equals(
                columnName,
                StringComparison.OrdinalIgnoreCase))
            {
                columnExists = true;
                break;
            }
        }

        reader.Close();

        if (columnExists)
        {
            return;
        }

        using var alterCommand = connection.CreateCommand();

        alterCommand.CommandText =
            $"ALTER TABLE UndoOperations " +
            $"ADD COLUMN {columnName} {columnDefinition};";

        alterCommand.ExecuteNonQuery();
    }

    public void Record(UndoOperation operation)
    {
        using var connection = _databaseConnection.CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO UndoOperations
            (
                OperationId,
                FolderPath,
                OriginalPath,
                NewPath,
                ExecutedAt,
                ReplacedFileBackupPath
            )
            VALUES
            (
                $operationId,
                $folderPath,
                $originalPath,
                $newPath,
                $executedAt,
                $replacedFileBackupPath
            );
            """;

        command.Parameters.AddWithValue(
            "$operationId",
            operation.OperationId);

        command.Parameters.AddWithValue(
            "$folderPath",
            operation.FolderPath);

        command.Parameters.AddWithValue(
            "$originalPath",
            operation.OriginalPath);

        command.Parameters.AddWithValue(
            "$newPath",
            operation.NewPath);

        command.Parameters.AddWithValue(
            "$executedAt",
            operation.ExecutedAt.ToString("O"));

        command.Parameters.AddWithValue(
            "$replacedFileBackupPath",
            operation.ReplacedFileBackupPath ?? string.Empty);

        command.ExecuteNonQuery();
    }

    public List<UndoOperation> GetLatestOperation(
        string folderPath)
    {
        using var connection = _databaseConnection.CreateConnection();

        connection.Open();

        string? operationId;

        using (var operationCommand = connection.CreateCommand())
        {
            operationCommand.CommandText = """
                SELECT OperationId
                FROM UndoOperations
                WHERE OperationId <> ''
                  AND FolderPath = $folderPath
                ORDER BY Id DESC
                LIMIT 1;
                """;

            operationCommand.Parameters.AddWithValue(
                "$folderPath",
                folderPath);

            operationId = operationCommand
                .ExecuteScalar()
                ?.ToString();
        }

        if (string.IsNullOrWhiteSpace(operationId))
        {
            return new List<UndoOperation>();
        }

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                OperationId,
                FolderPath,
                OriginalPath,
                NewPath,
                ExecutedAt,
                ReplacedFileBackupPath
            FROM UndoOperations
            WHERE OperationId = $operationId
              AND FolderPath = $folderPath
            ORDER BY Id DESC;
            """;

        command.Parameters.AddWithValue(
            "$operationId",
            operationId);

        command.Parameters.AddWithValue(
            "$folderPath",
            folderPath);

        using var reader = command.ExecuteReader();

        var operations = new List<UndoOperation>();

        while (reader.Read())
        {
            operations.Add(new UndoOperation
            {
                OperationId = reader.GetString(0),
                FolderPath = reader.GetString(1),
                OriginalPath = reader.GetString(2),
                NewPath = reader.GetString(3),
                ExecutedAt = DateTime.Parse(
                    reader.GetString(4)),
                ReplacedFileBackupPath = reader.IsDBNull(5)
                    ? string.Empty
                    : reader.GetString(5)
            });
        }

        return operations;
    }
}