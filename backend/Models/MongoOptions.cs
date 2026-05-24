namespace RealTimeBuzz.Models;

public sealed class MongoOptions
{
    public string ConnectionString { get; init; } = "mongodb://localhost:27017";
    public string Database { get; init; } = "realtimebuzz";
}
