namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Configurations;

public class FileLogConfiguration
{
    public string FolderPath { get; set; }
    public string MinLogLevel { get; set; }
    public string LogOutputTemplate { get; set; }
    public List<string> SpecificLogFolders { get; set; }

    public FileLogConfiguration()
    {
        FolderPath = string.Empty;
        MinLogLevel = "Information";
        LogOutputTemplate = "[{Timestamp:dd.MM.yyyy HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        SpecificLogFolders = new List<string>();
    }

    public FileLogConfiguration(string folderPath)
    {
        FolderPath = folderPath;
        MinLogLevel = "Information";
        LogOutputTemplate = "[{Timestamp:dd.MM.yyyy HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        SpecificLogFolders = new List<string>();
    }
}
