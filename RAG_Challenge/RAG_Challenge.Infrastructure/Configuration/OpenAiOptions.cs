namespace RAG_Challenge.Infrastructure.Configuration;

public sealed class OpenAiOptions
{
    public const string SectionName = "ExternalApis:OpenAI";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}