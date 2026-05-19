namespace DocumentManagement.Intelligence.Review;

public sealed class ReviewRuntimeCoordinator : IDisposable
{
    // Invariant: the runtime coordinator owns freshness and cancellation only.
    // It rejects stale work but does not rank evidence or decide semantic meaning.
    private readonly object _gate = new();
    private readonly Queue<ReviewRuntimeDiagnostic> _diagnostics = new();
    private CancellationTokenSource? _currentRefresh;
    private long _version;

    public ReviewRefreshRequest BeginRefresh()
    {
        lock (_gate)
        {
            TryCancel(_currentRefresh);

            _currentRefresh = new CancellationTokenSource();
            var version = ++_version;
            RecordLocked(new ReviewRuntimeDiagnostic(
                "refresh-started",
                version,
                "new refresh version started"));
            return new ReviewRefreshRequest(version, _currentRefresh);
        }
    }

    public bool IsCurrent(long version)
    {
        lock (_gate)
        {
            return version == _version;
        }
    }

    public ReviewRuntimeStateSnapshot CreateSnapshot()
    {
        lock (_gate)
        {
            return new ReviewRuntimeStateSnapshot(
                _version,
                _currentRefresh is not null,
                _diagnostics.ToArray());
        }
    }

    public ReviewRefreshCompletion Complete(ReviewRefreshRequest request, bool canceled = false)
    {
        lock (_gate)
        {
            var isCurrent = request.Version == _version && ReferenceEquals(_currentRefresh, request.Source);
            if (canceled || request.Token.IsCancellationRequested)
            {
                RecordLocked(new ReviewRuntimeDiagnostic(
                    "refresh-canceled",
                    request.Version,
                    "refresh canceled before state application"));
                return new ReviewRefreshCompletion(false, true, request.Version);
            }

            if (!isCurrent)
            {
                RecordLocked(new ReviewRuntimeDiagnostic(
                    "stale-refresh-rejected",
                    request.Version,
                    "stale refresh completion rejected"));
                return new ReviewRefreshCompletion(false, false, request.Version);
            }

            _currentRefresh = null;
            RecordLocked(new ReviewRuntimeDiagnostic(
                "refresh-applied",
                request.Version,
                "latest refresh completion accepted"));
            return new ReviewRefreshCompletion(true, false, request.Version);
        }
    }

    public ReviewRefreshCompletion RejectIfStale(ReviewRefreshRequest request, string reasonCode)
    {
        lock (_gate)
        {
            if (request.Version == _version && ReferenceEquals(_currentRefresh, request.Source) && !request.Token.IsCancellationRequested)
            {
                return new ReviewRefreshCompletion(true, false, request.Version);
            }

            RecordLocked(new ReviewRuntimeDiagnostic(
                reasonCode,
                request.Version,
                "stale runtime state discarded"));
            return new ReviewRefreshCompletion(false, request.Token.IsCancellationRequested, request.Version);
        }
    }

    public ReviewRefreshCompletion RejectIfStale(long version, string reasonCode)
    {
        lock (_gate)
        {
            if (version == _version)
            {
                return new ReviewRefreshCompletion(true, false, version);
            }

            RecordLocked(new ReviewRuntimeDiagnostic(
                reasonCode,
                version,
                "stale runtime state discarded"));
            return new ReviewRefreshCompletion(false, false, version);
        }
    }

    public void CancelRefresh()
    {
        lock (_gate)
        {
            if (_currentRefresh is null)
            {
                return;
            }

            TryCancel(_currentRefresh);
            RecordLocked(new ReviewRuntimeDiagnostic(
                "overlay-remap-canceled",
                _version,
                "active refresh and overlay remap canceled"));
        }
    }

    public IReadOnlyList<ReviewRuntimeDiagnostic> Diagnostics
    {
        get
        {
            lock (_gate)
            {
                return _diagnostics.ToArray();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            TryCancel(_currentRefresh);
            TryDispose(_currentRefresh);
            _currentRefresh = null;
        }
    }

    private void RecordLocked(ReviewRuntimeDiagnostic diagnostic)
    {
        _diagnostics.Enqueue(diagnostic);
        while (_diagnostics.Count > 16)
        {
            _diagnostics.Dequeue();
        }
    }

    private static void TryCancel(CancellationTokenSource? source)
    {
        if (source is null)
        {
            return;
        }

        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void TryDispose(CancellationTokenSource? source)
    {
        try
        {
            source?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}

public sealed record ReviewRefreshRequest(long Version, CancellationTokenSource Source) : IDisposable
{
    public CancellationToken Token => Source.Token;

    public void Dispose()
    {
        Source.Dispose();
    }
}

public sealed record ReviewRefreshCompletion(bool ShouldApply, bool WasCanceled, long Version);

public sealed record ReviewRuntimeDiagnostic(string Code, long Version, string Reason);

public sealed record ReviewRuntimeStateSnapshot(
    long CurrentVersion,
    bool HasActiveRefresh,
    IReadOnlyList<ReviewRuntimeDiagnostic> Diagnostics);
