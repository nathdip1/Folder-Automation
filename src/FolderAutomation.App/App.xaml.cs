using System.IO;
using System.Windows;
using FolderAutomation.Data;

namespace FolderAutomation;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string applicationDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderAutomation");

        Directory.CreateDirectory(applicationDataFolder);

        string databasePath = Path.Combine(
            applicationDataFolder,
            "FolderAutomation.db");

        var databaseInitializer = new DatabaseInitializer(databasePath);

        databaseInitializer.Initialize();
    }
}