using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace SupernovaLab.App.Services;

/// <summary>
/// Review M1: the last-resort error log for an exception that escapes every guard in normal (not --snapshot) mode. It
/// appends to error.log under %LOCALAPPDATA%\SupernovaLab and to the trace.
/// </summary>
public static class ErrorLog
{
    /// <summary>The default folder: %LOCALAPPDATA%\SupernovaLab.</summary>
    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SupernovaLab");

    /// <summary>
    /// Appends the time and the exception to error.log in <paramref name="directory"/> (default
    /// <see cref="DefaultDirectory"/>) and returns the file's path, or a note that only the trace has it when the file
    /// cannot be written.
    /// </summary>
    public static string Write(Exception exception, string? directory = null)
    {
        Trace.TraceError($"Unhandled: {exception}");
        string dir = directory ?? DefaultDirectory;
        string path = Path.Combine(dir, "error.log");
        try
        {
            Directory.CreateDirectory(dir);
            File.AppendAllText(path, $"{DateTime.Now.ToString("u", CultureInfo.InvariantCulture)} {exception}{Environment.NewLine}");
            return path;
        }
        catch (Exception io) when (io is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return "the debug trace (the log file could not be written)";
        }
    }
}
