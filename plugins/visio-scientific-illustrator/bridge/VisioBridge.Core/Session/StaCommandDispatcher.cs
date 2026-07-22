using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VisioBridge.Core.Session;

public sealed class StaCommandDispatcher : IDisposable
{
    private const uint WmQuit = 0x0012;
    private readonly object _gate = new();
    private readonly TaskCompletionSource _ready = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private Control _control = null!;
    private uint _nativeThreadId;
    private bool _disposed;

    public StaCommandDispatcher()
    {
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "Visio COM STA",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Task.GetAwaiter().GetResult();
    }

    public Task InvokeAsync(
        Action action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return InvokeAsync<object?>(() =>
        {
            action();
            return null;
        }, cancellationToken);
    }

    public Task<T> InvokeAsync<T>(
        Func<T> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _control.BeginInvoke(new Action(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.SetCanceled(cancellationToken);
                    return;
                }

                try
                {
                    var result = action();
                    completion.SetResult(result);
                }
                catch (Exception error)
                {
                    completion.SetException(error);
                }
            }));
        }

        return completion.Task;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (!PostThreadMessage(_nativeThreadId, WmQuit, UIntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        if (!_thread.Join(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("Visio COM STA 线程未在 5 秒内退出。");
        }
    }

    private void ThreadMain()
    {
        try
        {
            _nativeThreadId = GetCurrentThreadId();
            _control = new Control();
            _control.CreateControl();
            _ = _control.Handle;
            _ready.SetResult();
            Application.Run();
        }
        catch (Exception error)
        {
            _ready.TrySetException(error);
        }
        finally
        {
            _control?.Dispose();
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(
        uint threadId,
        uint message,
        UIntPtr wParam,
        IntPtr lParam);
}
