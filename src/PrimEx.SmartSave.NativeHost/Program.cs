using System.IO.Pipes;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.FileManager.ServerDiscovery;

// stdout is exclusively binary Native Messaging. Never print console diagnostics here.
using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
try
{
    string? Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    string data = Arg("--data-dir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimEx", "SmartSave");
    string central = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PrimEx", "SmartSave", "appsettings.json");
    var options = AppOptions.Load(Arg("--config") ?? (File.Exists(central) ? central : Path.Combine(data, "appsettings.json")));
    string origin = args.FirstOrDefault() ?? "";
    while (await NativeProtocol.ReadAsync<NativeMessage>(input) is { } message)
    {
        NativeReply reply;
        try
        {
            var envelope = new NativeEnvelope(origin, message); NativeProtocol.Validate(envelope, options);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await using var pipe = new NamedPipeClientStream(".", AgentPipeName.ForCurrentSession(data), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(deadline.Token);
            await NativeProtocol.WriteAsync(pipe, envelope, deadline.Token);
            reply = await NativeProtocol.ReadAsync<NativeReply>(pipe, deadline.Token) ?? new(false, "AgentUnavailable");
        }
        catch (OperationCanceledException) { reply = new(false, "AgentUnavailable"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException) { reply = new(false, e is UnauthorizedAccessException ? "RequestDenied" : "InvalidOrUnavailable"); }
        await NativeProtocol.WriteAsync(output, reply);
    }
    return 0;
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
{
    await NativeProtocol.WriteAsync(output, new NativeReply(false, "HostConfigurationOrProtocolError")); return 1;
}
