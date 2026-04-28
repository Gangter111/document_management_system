using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace DocumentManagement.Application.Services;

public class CachedDocumentService : ICachedDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "documents_list";
    private const string CacheKeyPrefix = "document_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public CachedDocumentService(IDocumentRepository documentRepository, IMemoryCache cache)
    {
        _documentRepository = documentRepository;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Document>> GetCachedDocumentsAsync()
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<Document>? cachedDocuments))
        {
            return cachedDocuments!;
        }

        var documents = await _documentRepository.GetAllAsync();
        
        _cache.Set(CacheKey, documents, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            SlidingExpiration = TimeSpan.FromMinutes(2),
            Size = documents.Count
        });

        return documents;
    }

    public async Task<Document?> GetCachedDocumentByIdAsync(long id)
    {
        var cacheKey = $"{CacheKeyPrefix}{id}";

        if (_cache.TryGetValue(cacheKey, out Document? cachedDocument))
        {
            return cachedDocument;
        }

        var document = await _documentRepository.GetByIdAsync(id);

        if (document != null)
        {
            _cache.Set(cacheKey, document, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });
        }

        return document;
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }
}