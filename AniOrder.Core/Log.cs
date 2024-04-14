namespace AniOrder.Core;

public enum LogLevel
{
    TRACE,
    INFO
}

public static class Log
{
    public static bool IsEnabled { get; private set; }
    public static LogLevel MinimumLevel { get; private set; }
    public static string FileName { get; private set; } = string.Empty;
    private static bool s_WriteToFile = false;

    public static void Init(bool isEnabled, LogLevel minimumLevel = LogLevel.INFO, string? fileName = null, bool deletePreviousFile = false)
    {
        IsEnabled = isEnabled;
        MinimumLevel = minimumLevel;
        if (!string.IsNullOrEmpty(fileName))
        {
            FileName = fileName;
            s_WriteToFile = true;
            string? directory = Path.GetDirectoryName(FileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            if (deletePreviousFile)
                File.Delete(FileName);
        }
    }

    public static void WriteLine(LogLevel level = LogLevel.INFO)
    {
        if (IsEnabled && (int)level >= (int)MinimumLevel)
        {
            Console.WriteLine();
            WriteToFile("\n");
        }
    }

    public static void WriteLine(object value, LogLevel level = LogLevel.INFO)
    {
        if (IsEnabled && (int)level >= (int)MinimumLevel)
        {
            Console.WriteLine(value);
            WriteToFile($"{value}\n");
        }
    }

    public static void Write(object value, LogLevel level = LogLevel.INFO)
    {
        if (IsEnabled && (int)level >= (int)MinimumLevel)
        {
            Console.Write(value);
            string? valueStr = value.ToString();
            WriteToFile(string.IsNullOrEmpty(valueStr) ? " " : valueStr);
        }
    }

    private static void WriteToFile(string value)
    {
        if (s_WriteToFile)
            File.AppendAllText(FileName, value);
    }
}
