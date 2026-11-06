using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

/// <summary>
/// One TaskIsland per user (PLAN.md section 7.7). A named mutex decides who is first; a later launch sends "EXPAND" over a
/// named pipe that only the same user can open (<see cref="PipeOptions.CurrentUserOnly"/>, section 7.11 rule 4) and exits.
/// The pipe is local only: no network.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const string BaseName = "TaskIsland-7F3A9C2E";
    public const string ExpandMessage = "EXPAND";
    private const int MaxMessageBytes = 16;

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private Mutex? _mutex;

    /// <param name="scope">Empty for the real app. Test runs with their own data folder pass a scope, so they never meet
    /// the real app or each other.</param>
    public SingleInstance(string scope = "")
    {
        string suffix = string.IsNullOrEmpty(scope) ? "" : "-" + scope;
        _mutexName = $@"Local\{BaseName}-SingleInstance{suffix}";
        _pipeName = $"{BaseName}-{CurrentUserSid()}{suffix}";
    }

    public string MutexName => _mutexName;

    /// <summary>True when this process is the first instance (and now owns the mutex).</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out bool createdNew);
        if (createdNew) return true;
        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    /// <summary>
    /// Second instance: lets the first one take the foreground (the user just launched us, so we may pass that right on),
    /// then sends EXPAND. Retries for up to ~3 s in case the first instance is still starting.
    /// </summary>
    public bool SignalFirstInstance()
    {
        User32.AllowSetForegroundWindow(User32.ASFW_ANY);
        var bytes = Encoding.ASCII.GetBytes(ExpandMessage);
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
                client.Connect(500);
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return true;
            }
            catch (TimeoutException) { }
            catch (IOException) { Thread.Sleep(250); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException
                                           or InvalidOperationException or ArgumentException or PlatformNotSupportedException)
            {
                return false;   // the pipe can't be used at all; retrying won't help and the caller just exits
            }
        }
        return false;
    }

    /// <summary>
    /// First instance: listens in the background (an idle pipe wait costs no CPU). <paramref name="onExpand"/> runs on a
    /// thread-pool thread; the caller marshals it to the UI thread. Anything other than the exact EXPAND message is ignored.
    /// </summary>
    public void StartServer(Action onExpand, Action<string>? log = null)
    {
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    var buffer = new byte[MaxMessageBytes + 1];
                    int total = 0, read;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    while (total < buffer.Length && (read = await server.ReadAsync(buffer.AsMemory(total), timeout.Token).ConfigureAwait(false)) > 0)
                        total += read;
                    if (total <= MaxMessageBytes && Encoding.ASCII.GetString(buffer, 0, total) == ExpandMessage) onExpand();
                    else log?.Invoke($"single instance: ignored a pipe message of {total} bytes");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    log?.Invoke($"single instance: pipe error {ex.GetType().Name}: {ex.Message}");
                    try { await Task.Delay(1000, token).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
            _mutex = null;
        }
    }

    private static string CurrentUserSid()
    {
        using var id = WindowsIdentity.GetCurrent();
        return id.User?.Value ?? Environment.UserName;
    }
}
