namespace ProductsMicroservice.Core.Domain.RepositoryContracts;

public interface IUnitOfWork
{
    /// <summary>Discards tracked changes after a failed operation before reading a concurrent result.</summary>
    void DiscardPendingChanges();

    /// <summary>Persists every change tracked by the current request as one atomic unit.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
