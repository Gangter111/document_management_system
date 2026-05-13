using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Documents;

namespace DocumentManagement.Wpf.Services;

public sealed class DocumentSearchCoordinator : IDisposable
{
    private readonly ApiService _apiService;
    private readonly object _gate = new();
    private CancellationTokenSource? _currentSearch;
    private long _version;

    public DocumentSearchCoordinator(ApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<DocumentSearchResult> SearchAsync(
        DocumentSearchCriteria criteria,
        TimeSpan? debounce = null)
    {
        var request = CreateRequest();

        try
        {
            if (debounce.HasValue && debounce.Value > TimeSpan.Zero)
            {
                await Task.Delay(debounce.Value, request.Token);
            }

            var result = await _apiService.SearchDocumentsAsync(
                criteria.Keyword,
                criteria.CategoryId,
                criteria.StatusId,
                criteria.Urgency,
                criteria.FromDate,
                criteria.ToDate,
                criteria.PageNumber,
                criteria.PageSize,
                request.Token);

            return IsCurrent(request.Version)
                ? DocumentSearchResult.Success(result)
                : DocumentSearchResult.Stale();
        }
        catch (OperationCanceledException)
        {
            return DocumentSearchResult.Stale();
        }
        finally
        {
            Complete(request);
            request.Dispose();
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _currentSearch?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _currentSearch?.Cancel();
            _currentSearch = null;
        }
    }

    private SearchRequest CreateRequest()
    {
        lock (_gate)
        {
            _currentSearch?.Cancel();

            _currentSearch = new CancellationTokenSource();
            var version = ++_version;

            return new SearchRequest(version, _currentSearch);
        }
    }

    private bool IsCurrent(long version)
    {
        lock (_gate)
        {
            return version == _version;
        }
    }

    private void Complete(SearchRequest request)
    {
        lock (_gate)
        {
            if (request.Version == _version && ReferenceEquals(_currentSearch, request.Source))
            {
                _currentSearch = null;
            }
        }
    }

    private sealed class SearchRequest : IDisposable
    {
        private readonly CancellationTokenSource _source;

        public SearchRequest(long version, CancellationTokenSource source)
        {
            Version = version;
            _source = source;
        }

        public long Version { get; }

        public CancellationToken Token => _source.Token;

        public CancellationTokenSource Source => _source;

        public void Dispose()
        {
            _source.Dispose();
        }
    }
}

public sealed record DocumentSearchCriteria(
    string? Keyword,
    long? CategoryId,
    long? StatusId,
    string? Urgency,
    DateTime? FromDate,
    DateTime? ToDate,
    int PageNumber,
    int PageSize);

public sealed class DocumentSearchResult
{
    private DocumentSearchResult(bool isStale, PagedResultDto<DocumentDto>? result)
    {
        IsStale = isStale;
        Result = result;
    }

    public bool IsStale { get; }

    public PagedResultDto<DocumentDto>? Result { get; }

    public static DocumentSearchResult Success(PagedResultDto<DocumentDto> result)
    {
        return new DocumentSearchResult(false, result);
    }

    public static DocumentSearchResult Stale()
    {
        return new DocumentSearchResult(true, null);
    }
}
