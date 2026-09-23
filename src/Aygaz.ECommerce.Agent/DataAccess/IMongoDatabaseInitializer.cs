namespace Aygaz.ECommerce.Agent.DataAccess;

public interface IMongoDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
