using Microsoft.Data.Sqlite;

namespace FolderAutomation.Data;

public class DatabaseInitializer
{
    private readonly DatabaseConnection _databaseConnection;

    public DatabaseInitializer(string databasePath)
    {
        _databaseConnection = new DatabaseConnection(databasePath);
    }

    public void Initialize()
    {
        using var connection = _databaseConnection.CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS AppSettings
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SettingName TEXT NOT NULL UNIQUE,
                SettingValue TEXT
            );
            """;

        command.ExecuteNonQuery();
    }
}