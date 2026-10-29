using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Keeps one primary Mimi window per Windows user and forwards file-open
/// requests from later Explorer launches to that window through a named
/// pipe. This avoids several processes competing for playback, the SQLite
/// library, settings, and watched-folder scans.
/// </summary>
internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly string mutexName;
    private readonly string pipeName;
    private Mutex? mutex;
    private bool ownsMutex;
    private bool disposed;
    private CancellationTokenSource? listenerCancellation;
    private Task? listenerTask;

    public SingleInstanceCoordinator()
    {
        var userScope = GetUserScope();
        mutexName = $@"Local\Mimi.SingleInstance.{userScope}";
        pipeName = $"Mimi.OpenPaths.{userScope}";
    }

    public bool IsPrimaryInstance => ownsMutex;

    public bool TryAcquirePrimary()
    {
        if (disposed) return false;
        if (ownsMutex) return true;

        mutex ??= new Mutex(initiallyOwned: false, mutexName);
        try
        {
            ownsMutex = mutex.WaitOne(0, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            // The former primary exited without releasing the mutex (for
            // example after a crash); ownership is now transferred here.
            ownsMutex = true;
            Log.Warning("Recovered Mimi's abandoned single-instance mutex");
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        return ownsMutex;
    }

    public bool TryForwardPaths(IReadOnlyList<string> paths)
    {
        if (disposed || ownsMutex) return false;

        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.Out,
                PipeOptions.None);
            client.Connect(5000);

            using var writer = new StreamWriter(client, Encoding.UTF8);
            writer.Write(JsonSerializer.Serialize(paths));
            writer.Flush();
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            TimeoutException or
            UnauthorizedAccessException or
            ObjectDisposedException)
        {
            Log.Warning(
                ex,
                "Could not forward an external open request to the primary Mimi instance");
            return false;
        }
    }

    public void StartListening(Func<IReadOnlyList<string>, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (disposed || !ownsMutex || listenerTask is not null) return;

        listenerCancellation = new CancellationTokenSource();
        listenerTask = ListenAsync(handler, listenerCancellation.Token);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        listenerCancellation?.Cancel();
        listenerCancellation?.Dispose();
        listenerCancellation = null;

        if (mutex is not null)
        {
            if (ownsMutex)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Mutexes are thread-affine. Normal shutdown releases on
                    // the UI thread that acquired it; this guard only covers
                    // unusual teardown paths.
                }
                finally
                {
                    ownsMutex = false;
                }
            }

            mutex.Dispose();
            mutex = null;
        }
    }

    private async Task ListenAsync(
        Func<IReadOnlyList<string>, Task> handler,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(
                    server,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var paths = string.IsNullOrWhiteSpace(payload)
                    ? []
                    : JsonSerializer.Deserialize<string[]>(payload) ?? [];

                await handler(paths);
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Mimi's single-instance listener failed");
                try
                {
                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    private static string GetUserScope()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value;
            if (!string.IsNullOrWhiteSpace(sid))
                return sid;
        }
        catch (Exception ex) when (
            ex is System.Security.SecurityException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            // Fall back below; single-instance support is best effort.
        }

        var userName = Environment.UserName;
        return string.IsNullOrWhiteSpace(userName) ? "DefaultUser" : userName;
    }
}
