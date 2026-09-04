using System.Text;

namespace WinPieGestures.WinUI.Services;

internal static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarPie", "logs");
    private static readonly string FilePath = Path.Combine(Folder, "winui3.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Folder);
                StringBuilder line = new()
                {
                    Capacity = message.Length + 96
                };
                line.Append(DateTimeOffset.Now.ToString("O"));
                line.Append(" [").Append(level).Append("] ").Append(message);
                if (exception is not null)
                {
                    line.AppendLine().Append(exception);
                }
                line.AppendLine();
                File.AppendAllText(FilePath, line.ToString());
            }
        }
        catch
        {
            // Logging must never interfere with input processing.
        }
    }
}
