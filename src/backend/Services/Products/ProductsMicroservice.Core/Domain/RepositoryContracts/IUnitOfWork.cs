namespace ProductsMicroservice.Core.Domain.RepositoryContracts;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists every change tracked by the current request as one atomic unit.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
