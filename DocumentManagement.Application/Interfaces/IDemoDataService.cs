namespace DocumentManagement.Application.Interfaces;

public interface IDemoDataService
{
    Task<int> EnsureSeededAsync(CancellationToken cancellationToken = default);

    Task<int> ClearAsync(CancellationToken cancellationToken = default);

    Task<int> GetActiveDemoCountAsync(CancellationToken cancellationToken = default);
}
