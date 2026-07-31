using System.Diagnostics;
using System.IO.Pipes;

namespace CodexUsageMonitor.Services;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\RHPLUSSEUNG.CodexUsageMonitor";
    private const byte ActivateSettingsCommand = 1;
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _serverLock = new();
    private NamedPipeServerStream? _server;
    private Task? _listenTask;
    private bool _disposed;

    public SingleInstanceCoordinator(
        string mutexName = MutexName,
        string? pipeName = null)
    {
        _mutex = new Mutex(
            initiallyOwned: true,
            name: mutexName,
            createdNew: out bool createdNew);
        IsPrimary = createdNew;
        _pipeName = pipeName
                    ?? $"RHPLUSSEUNG.CodexUsageMonitor.{Process.GetCurrentProcess().SessionId}";
    }

    public bool IsPrimary { get; }

    public void StartListening(Action activationRequested)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary)
            throw new InvalidOperationException("Only the primary instance can listen for activation.");
        ArgumentNullException.ThrowIfNull(activationRequested);
        if (_listenTask is not null)
            return;

        _listenTask = Task.Run(() => ListenAsync(activationRequested, _cancellation.Token));
    }

    public bool NotifyPrimary()
    {
        if (IsPrimary)
            return false;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                client.Connect(timeout: 400);
                client.WriteByte(ActivateSettingsCommand);
                client.Flush();
                return true;
            }
            catch (Exception exception) when (
                exception is TimeoutException or IOException)
            {
                Thread.Sleep(100);
            }
        }

        return false;
    }

    private async Task ListenAsync(Action activationRequested, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                lock (_serverLock)
                    _server = server;

                await server.WaitForConnectionAsync(cancellationToken);
                var command = new byte[1];
                int read = await server.ReadAsync(command, cancellationToken);
                if (read == 1 && command[0] == ActivateSettingsCommand)
                    activationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                if (!cancellationToken.IsCancellationRequested)
                    await Task.Delay(100, cancellationToken);
            }
            finally
            {
                lock (_serverLock)
                    _server = null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cancellation.Cancel();
        lock (_serverLock)
            _server?.Dispose();
        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException exception) when (
            exception.InnerExceptions.All(item => item is OperationCanceledException))
        {
        }

        _cancellation.Dispose();
        if (IsPrimary)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
