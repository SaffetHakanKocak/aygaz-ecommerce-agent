namespace Aygaz.ECommerce.Agent.DataAccess;

public interface IRelationalDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
