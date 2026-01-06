namespace RAG_Challenge.Infrastructure.Configuration;

public sealed class OpenAiOptions
{
    public const string EmbeddingsPath = "embeddings";
    public const string ChatCompletionsPath = "chat/completions";
    public const string EmbeddingModel = "text-embedding-3-large";
    public const string ChatModel = "gpt-4o";
    
    public const string SectionName = "ExternalApis:OpenAI";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}