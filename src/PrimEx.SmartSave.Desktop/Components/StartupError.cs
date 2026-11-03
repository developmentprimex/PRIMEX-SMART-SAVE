using System.Text.Json;
namespace PrimEx.SmartSave.Desktop.Components;

public sealed record StartupError(string Code, string Message)
{
    public const string AlreadyRunning = "PrimEx Smart Save is already running in the notification area near the Windows clock.\n\nOpen the hidden icons arrow (^), right-click PrimEx Smart Save, and choose Exit. Then open this version again. Closing the main window only hides the app.\n\nA running transfer must finish or be cancelled before exiting. Your saved settings and pending operations are retained.";
    public static StartupError From(Exception exception, string stage)
    {
        if (exception is UnauthorizedAccessException)
            return new("AccessDenied", "Windows denied access while " + stage + ".\n\nCheck that your Windows account can read the settings and write its local PrimEx Smart Save data folder. A managed settings file may require your IT administrator. No permissions were changed.");
        if (exception is JsonException or InvalidDataException)
            return new("InvalidConfigurationOrState", "The settings or saved operation state could not be read while " + stage + ".\n\n" + exception.Message);
        int error = exception.HResult & 0xffff;
        if (exception is IOException && error is 32 or 33)
            return new("FileInUse", "A required file is in use while " + stage + ".\n\nClose the application editing that file, then try again. Your files and settings have not been reset.");
        return new("StartupStorageError", "PrimEx Smart Save could not finish " + stage + ".\n\nWindows error: " + error + " (" + exception.GetType().Name + "). Check that its application files and local data folder are available. Your settings and pending operations have not been reset.");
    }
}
