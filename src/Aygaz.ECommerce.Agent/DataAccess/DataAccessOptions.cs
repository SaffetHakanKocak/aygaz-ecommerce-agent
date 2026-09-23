namespace Aygaz.ECommerce.Agent.DataAccess;

public sealed class DataAccessOptions
{
    public const string SectionName = "DataAccess";

    public string Provider { get; set; } = "MongoDb";

    public MongoDbOptions MongoDb { get; set; } = new();

    public RelationalDataAccessOptions Sqlite { get; set; } = new();

    public RelationalDataAccessOptions SqlServer { get; set; } = new();
}

public sealed class MongoDbOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string DatabaseName { get; set; } = "aygaz-ecommerce";

    public bool SeedDummyData { get; set; }

    public bool AllowProductionSeed { get; set; }
}

public sealed class RelationalDataAccessOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}
