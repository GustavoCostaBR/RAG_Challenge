namespace RAG_Challenge.Infrastructure.Configuration;

public sealed class VectorDbOptions
{
    public const string SectionName = "ExternalApis:VectorDb";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string IndexName { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "2023-11-01";
}