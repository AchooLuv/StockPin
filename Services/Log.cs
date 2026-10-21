namespace StockPin.Services;

/// <summary>Tiny file logger; never throws.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine(ConfigService.Folder, "stockpin.log");

    public static void Warn(string message) => Write("WARN", message);
    public static void Info(string message) => Write("INFO", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(ConfigService.Folder);
                var file = new FileInfo(PathName);
                if (file.Exists && file.Length > 256 * 1024) file.Delete();
                File.AppendAllText(PathName,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
            // logging must never break the app
        }
    }
}
