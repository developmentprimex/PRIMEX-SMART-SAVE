using System.IO.Pipes;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.FileManager.ServerDiscovery;
namespace PrimEx.SmartSave.Desktop.Components;

// Single-instance UI activation only; never accepts file paths or transfer commands.
public sealed class AgentActivationService(string dataDirectory, Action showWindow) : IDisposable
{
    private sealed record ActivationRequest(string Command);
    private readonly CancellationTokenSource stopped = new();
    private Task? listener;
    private static string PipeName(string data) => AgentPipeName.ForCurrentSession(data) + ".activate";
    public void Start() => listener = Task.Run(ListenAsync);
    private async Task ListenAsync()
    {
        while (!stopped.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(PipeName(dataDirectory), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stopped.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
                var request = await NativeProtocol.ReadAsync<ActivationRequest>(pipe, timeout.Token);
                if (request?.Command != "show") { await NativeProtocol.WriteAsync(pipe, new NativeReply(false, "UnsupportedActivation"), timeout.Token); continue; }
                showWindow();
                await NativeProtocol.WriteAsync(pipe, new NativeReply(true, "Shown"), timeout.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
        }
    }
    public static async Task<bool> TryShowAsync(string dataDirectory)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
            await using var pipe = new NamedPipeClientStream(".", PipeName(dataDirectory), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await NativeProtocol.WriteAsync(pipe, new ActivationRequest("show"), timeout.Token).ConfigureAwait(false);
            return (await NativeProtocol.ReadAsync<NativeReply>(pipe, timeout.Token).ConfigureAwait(false))?.Ok == true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException or System.Text.Json.JsonException) { return false; }
    }
    public void Dispose() { stopped.Cancel(); if (listener?.IsCompleted == true) stopped.Dispose(); }
}
