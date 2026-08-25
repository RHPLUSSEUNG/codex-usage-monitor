using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.UI;

internal sealed class CompactBarHost : IDisposable
{
    private readonly Action<Action> _postToOwner;
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private CompactBarForm? _form;
    private ApplicationContext? _context;
    private bool _disposed;

    public event EventHandler? SettingsRequested;
    public event EventHandler? RefreshRequested;
    public event Action<Point>? PositionChanged;

    public CompactBarHost(Action<Action> postToOwner)
    {
        _postToOwner = postToOwner;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "CodexUsageMonitor.CompactBar"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }

    public void Apply(AppSettings settings, UsageSnapshot snapshot, SystemUsageSnapshot systemUsage) =>
        Post(form => form.Apply(settings.Copy(), snapshot, systemUsage));

    public void ApplyLanguage() => Post(form => form.ApplyLanguage());

    public void BeginPreview() => Post(form => form.BeginPreview());

    public void Preview(AppSettings settings) => Post(form => form.Preview(settings.Copy()));

    public void EndPreview(AppSettings settings) => Post(form =>
    {
        form.Preview(settings.Copy());
        form.ResumeRendering();
    });

    public void PositionWindow() => Post(form => form.PositionWindow());

    public void ResetPosition() => Post(form => form.ResetPosition());

    public void Close()
    {
        if (_disposed)
            return;
        Post(form => form.Close());
        _thread.Join(TimeSpan.FromSeconds(3));
    }

    private void ThreadMain()
    {
        var context = new ApplicationContext();
        var form = new CompactBarForm();
        _context = context;
        _form = form;
        _ = form.Handle;

        form.SettingsRequested += (_, _) => NotifyOwner(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        form.RefreshRequested += (_, _) => NotifyOwner(() => RefreshRequested?.Invoke(this, EventArgs.Empty));
        form.PositionChanged += (_, _) =>
        {
            Point position = form.StoredPosition;
            NotifyOwner(() => PositionChanged?.Invoke(position));
        };
        form.FormClosed += (_, _) => context.ExitThread();

        _ready.Set();
        Application.Run(context);
        _form = null;
        _context = null;
    }

    private void Post(Action<CompactBarForm> action)
    {
        CompactBarForm? form = _form;
        if (_disposed || form is null || form.IsDisposed || !form.IsHandleCreated)
            return;
        try
        {
            form.BeginInvoke(() => action(form));
        }
        catch (InvalidOperationException)
        {
            // The display thread is shutting down.
        }
    }

    private void NotifyOwner(Action action)
    {
        try
        {
            _postToOwner(action);
        }
        catch (InvalidOperationException)
        {
            // The tray thread is shutting down.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Close();
        _disposed = true;
        _ready.Dispose();
    }
}
