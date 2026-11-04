using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace PrimEx.SmartSave.Desktop.Components;

internal static class RequiredDestinationChoice
{
    public static void Attach(Window window, Func<bool> chosen)
    {
        HideCloseButton(window);
        window.Closing += (_, e) => {
            if (!chosen() && !window.Dispatcher.HasShutdownStarted && Application.Current is not App { IsExiting: true }) e.Cancel = true;
        };
    }
    public static void HideCloseButton(Window window)
    {
        window.SourceInitialized += (_, _) => {
            var handle = new WindowInteropHelper(window).Handle;
            SetWindowLong(handle, -16, GetWindowLong(handle, -16) & ~0x00080000); // WS_SYSMENU
            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x0037); // Refresh only the non-client frame.
        };
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
