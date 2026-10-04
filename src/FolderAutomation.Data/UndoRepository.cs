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
                OriginalPath TEXT NOT NULL,
                NewPath TEXT NOT NULL,
                ExecutedAt TEXT NOT NULL
            );
            """;

        createTableCommand.ExecuteNonQuery();

        AddOperationIdColumnIfNeeded(connection);
    }

    private static void AddOperationIdColumnIfNeeded(
        SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        command.CommandText = """
            PRAGMA table_info(UndoOperations);
            """;

        using var reader = command.ExecuteReader();

        bool operationIdExists = false;

        while (reader.Read())
        {
            string columnName = reader.GetString(1);

            if (columnName.Equals(
                "OperationId",
                StringComparison.OrdinalIgnoreCase))
            {
                operationIdExists = true;
                break;
            }
        }

        reader.Close();

        if (operationIdExists)
        {
            return;
        }

        using var alterCommand = connection.CreateCommand();

        alterCommand.CommandText = """
            ALTER TABLE UndoOperations
            ADD COLUMN OperationId TEXT NOT NULL DEFAULT '';
            """;

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
                OriginalPath,
                NewPath,
                ExecutedAt
            )
            VALUES
            (
                $operationId,
                $originalPath,
                $newPath,
                $executedAt
            );
            """;

        command.Parameters.AddWithValue(
            "$operationId",
            operation.OperationId);

        command.Parameters.AddWithValue(
            "$originalPath",
            operation.OriginalPath);

        command.Parameters.AddWithValue(
            "$newPath",
            operation.NewPath);

        command.Parameters.AddWithValue(
            "$executedAt",
            operation.ExecutedAt.ToString("O"));

        command.ExecuteNonQuery();
    }

    public List<UndoOperation> GetLatestOperation()
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
                ORDER BY Id DESC
                LIMIT 1;
                """;

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
                OriginalPath,
                NewPath,
                ExecutedAt
            FROM UndoOperations
            WHERE OperationId = $operationId
            ORDER BY Id DESC;
            """;

        command.Parameters.AddWithValue(
            "$operationId",
            operationId);

        using var reader = command.ExecuteReader();

        var operations = new List<UndoOperation>();

        while (reader.Read())
        {
            operations.Add(new UndoOperation
            {
                OperationId = reader.GetString(0),
                OriginalPath = reader.GetString(1),
                NewPath = reader.GetString(2),
                ExecutedAt = DateTime.Parse(
                    reader.GetString(3))
            });
        }

        return operations;
    }
}