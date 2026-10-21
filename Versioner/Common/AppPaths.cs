using System.IO;

namespace Versioner.Common;

public static class AppPaths
{
    public const string AppName = "Versioner";

    public static string AppDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppName);

    public static string LogsDirectory { get; } = Path.Combine(AppDataRoot, "logs");
    public static string LogFilePath { get; } = Path.Combine(LogsDirectory, "log-.txt");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsDirectory);
    }
}
