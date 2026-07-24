// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.IO.Pipes;
using System.Text.Json;

namespace Browse.Services;

/// <summary>
/// Forwards secondary process launch arguments to the running Browse process.
/// </summary>
/// <remarks>
/// The single-instance mutex rejects duplicate processes while this channel preserves shell and command-line launches.
/// </remarks>
internal sealed class SingleInstanceLaunchServer : IDisposable
{
    private const string DefaultPipeName = "Browse.SingleInstance.Launch";
    private readonly string m_pipeName;
    private readonly Action<IReadOnlyList<string>> m_launch;
    private readonly CancellationTokenSource m_cancellation = new();
    private readonly Task m_listener;

    public SingleInstanceLaunchServer(Action<IReadOnlyList<string>> launch, string pipeName = DefaultPipeName)
    {
        m_launch = launch;
        m_pipeName = pipeName;
        m_listener = Task.Run(ListenAsync);
    }

    public static bool TrySend(IReadOnlyList<string> arguments, string pipeName = DefaultPipeName)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            client.Connect(5_000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(JsonSerializer.Serialize(arguments));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task ListenAsync()
    {
        while (!m_cancellation.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    m_pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(m_cancellation.Token).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var json = await reader.ReadLineAsync(m_cancellation.Token).ConfigureAwait(false);
                var arguments = JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? [];
                m_launch(arguments);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                await Task.Delay(100, m_cancellation.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        m_cancellation.Cancel();
        try
        {
            m_listener.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        m_cancellation.Dispose();
    }
}
