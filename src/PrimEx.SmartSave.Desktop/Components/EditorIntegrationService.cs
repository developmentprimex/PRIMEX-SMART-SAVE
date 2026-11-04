using System.Diagnostics;

namespace PrimEx.SmartSave.Desktop.Components;

public sealed class EditorIntegrationService
{
    private static string Bundle(string editor)
    {
        string version = typeof(App).Assembly.GetName().Version!.ToString(3);
        string path = Path.Combine(AppContext.BaseDirectory, "EditorIntegrations", $"PrimEx.SmartSave-{editor}-{version}.vsix");
        if (!File.Exists(path)) throw new InvalidOperationException("Install the current staff MSI first; the editor links are included with it.");
        return path;
    }
    public static ProcessStartInfo CreateVSCodeInstallStart(string cli, string vsix) => new(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
    {
        Arguments = $"/d /s /c \"\"{cli}\" --install-extension \"{vsix}\" --force\"",
        UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
        RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(cli)!
    };
    public async Task InstallVSCodeAsync()
    {
        string vsix = Bundle("VSCode");
        string[] bases = [Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)];
        string? cli = bases.SelectMany(root => new[] { Path.Combine(root, "Microsoft VS Code", "bin", "code.cmd"), Path.Combine(root, "Microsoft VS Code Insiders", "bin", "code-insiders.cmd") }).FirstOrDefault(File.Exists);
        if (cli == null) throw new InvalidOperationException("VS Code was not found in its standard install locations. Use Extensions: Install from VSIX in VS Code and select the VSCode link from the installed EditorIntegrations folder.");
        using var process = Process.Start(CreateVSCodeInstallStart(cli, vsix))!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(); throw new TimeoutException("VS Code installation did not finish. Open VS Code and use Install from VSIX."); }
        await Task.WhenAll(output, errors);
        if (process.ExitCode != 0) throw new InvalidOperationException("VS Code could not install the link. Use Extensions: Install from VSIX in VS Code, then select the VSCode link from EditorIntegrations.");
    }
    public async Task LaunchVisualStudioInstallerAsync()
    {
        string vsix = Bundle("VisualStudio");
        string finder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(finder)) throw new InvalidOperationException("Visual Studio 2022 17.14+ or Visual Studio 2026 x64 was not found. Install the VisualStudio link once on a PC with that editor.");
        var start = new ProcessStartInfo(finder) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true };
        foreach (string value in new[] { "-latest", "-products", "*", "-version", "[17.14,19.0)", "-property", "installationPath" }) start.ArgumentList.Add(value);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(); throw new TimeoutException("Visual Studio discovery did not finish. Open the VisualStudio VSIX from EditorIntegrations manually."); }
        string directory = (await output).Trim();
        string installer = Path.Combine(directory, "Common7", "IDE", "VSIXInstaller.exe");
        if (directory.Length == 0 || !File.Exists(installer)) throw new InvalidOperationException("A supported Visual Studio installation was not found. Use the VisualStudio VSIX on a PC with Visual Studio 2022 17.14+ or 2026 x64.");
        // This installer is intentionally interactive so the user can complete it.
        var install = new ProcessStartInfo(installer) { UseShellExecute = true };
        install.ArgumentList.Add(vsix); Process.Start(install)?.Dispose();
    }
}
