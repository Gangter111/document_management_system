namespace DocumentManagement.Wpf.Services;

public sealed class OperationTracker
{
    private int _activeOperations;

    public event EventHandler? IsBusyChanged;

    public bool IsBusy => _activeOperations > 0;

    public IDisposable Begin()
    {
        var newValue = Interlocked.Increment(ref _activeOperations);
        if (newValue == 1)
        {
            IsBusyChanged?.Invoke(this, EventArgs.Empty);
        }

        return new Scope(this);
    }

    private void End()
    {
        var newValue = Interlocked.Decrement(ref _activeOperations);
        if (newValue == 0)
        {
            IsBusyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class Scope : IDisposable
    {
        private OperationTracker? _owner;

        public Scope(OperationTracker owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.End();
        }
    }
}
