using System.IO.Pipes;
using System.Windows.Threading;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.Desktop.ViewModels;
using PrimEx.SmartSave.FileManager.ServerDiscovery;
namespace PrimEx.SmartSave.Desktop.Components;
public sealed class NativePipeServer(AppOptions options, string dataDirectory, QueueViewModel queue, Dispatcher dispatcher, IEventLog log, Action<Guid> requestQueued) : IDisposable
{
    private readonly CancellationTokenSource stopped = new();
    private Task? listener;
    public void Start() => listener = Task.Run(ListenAsync);
    private async Task ListenAsync()
    {
        while (!stopped.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(AgentPipeName.ForCurrentSession(dataDirectory), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stopped.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped.Token); deadline.CancelAfter(TimeSpan.FromSeconds(5));
                var envelope = await NativeProtocol.ReadAsync<NativeEnvelope>(pipe, deadline.Token);
                NativeReply reply;
                try
                {
                    if (envelope == null) throw new InvalidDataException();
                    NativeProtocol.Validate(envelope, options);
                    var message = envelope.Message;
                    log.Write("BrowserConnectionEstablished");
                    if (message.Command == "ping") reply = new(true, "Ready");
                    else if (message.Command == "status")
                        reply = await dispatcher.InvokeAsync(() => new NativeReply(true, "Status", queue.Operations.FirstOrDefault(x => x.Request.Id == message.Id)?.State.ToString() ?? "Unknown"));
                    else
                    {
                        // Duplicate completion events remain harmless even after the source has moved.
                        bool known = await dispatcher.InvokeAsync(() => queue.Operations.Any(x => x.Request.Id == message.Id));
                        if (!known) NativeProtocol.ValidateCompletedFile(message);
                        var request = new SaveRequest(message.Id, message.SourcePath!, Path.GetFileName(message.SourcePath!), "Browser", message.CreatedUtc);
                        bool added = await dispatcher.InvokeAsync(() => queue.Enqueue(request));
                        reply = new(true, "Queued");
                        if (added)
                        {
                            log.Write("DownloadDetected", request.Id);
                            _ = dispatcher.InvokeAsync(() => requestQueued(request.Id));
                        }
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
                { reply = new(false, "RequestDeniedOrFileUnavailable"); log.Write("BrowserRequestRejected", null, e.GetType().Name); }
                await NativeProtocol.WriteAsync(pipe, reply, deadline.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or UnauthorizedAccessException or System.Text.Json.JsonException)
            { if (!stopped.IsCancellationRequested) log.Write("BrowserPipeError", null, e.GetType().Name); }
        }
    }
    public void Dispose() { stopped.Cancel(); if (listener?.IsCompleted == true) stopped.Dispose(); }
}
