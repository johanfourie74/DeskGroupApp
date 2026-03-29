using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeskGroup.Helpers;

/// <summary>
/// Lightweight named-pipe IPC so a shell-context-menu-launched second instance can
/// forward its <c>--add-to-group</c> path to the already-running DeskGroup process.
/// </summary>
public sealed class NamedPipeServer : IDisposable
{
    private const string PipeName = "DeskGroup_IPC_v1";

    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>Raised on the thread-pool when a message arrives. Marshal to UI as needed.</summary>
    public event EventHandler<string>? MessageReceived;

    /// <summary>Starts the background listener loop.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var message = await reader.ReadToEndAsync(token);

                    if (!string.IsNullOrWhiteSpace(message))
                        MessageReceived?.Invoke(this, message.Trim());
                }
                catch (OperationCanceledException) { break; }
                catch { /* restart listener on transient errors */ }
            }
        }, token);
    }

    /// <summary>
    /// Sends <paramref name="message"/> to the running instance.
    /// Returns <c>true</c> on success.
    /// </summary>
    public static bool TrySend(string message, int timeoutMs = 3000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.Write(message);
            return true;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
