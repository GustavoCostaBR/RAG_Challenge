namespace RAG_Challenge.Domain;

// External API models
public record OpenAiItemResponse(string Id, string Name, string Status);
public record VectorDbSearchResult(string Content, string Type, float? Score);

public record EmbeddingRequest(string Model, string Input);
public record EmbeddingData(string Object, int Index, IReadOnlyList<float> Embedding);
public record EmbeddingResponse(string Object, IReadOnlyList<EmbeddingData> Data, string Model, EmbeddingUsage Usage);
public record EmbeddingUsage(int PromptTokens, int TotalTokens);

public record ChatMessage(string Role, string Content);
public record ChatCompletionRequest(string Model, IReadOnlyList<ChatMessage> Messages);
public record ChatChoice(ChatMessage Message, string? FinishReason);
public record ChatCompletionResponse(string Id, string Object, long Created, string Model, IReadOnlyList<ChatChoice> Choices);

public record VectorQuery(float[] Vector, int K, string Fields, string Kind = "vector");
public record VectorSearchRequest(bool Count, string Select, int Top, string? Filter, IReadOnlyList<VectorQuery> VectorQueries);
public record VectorSearchResponse(IReadOnlyList<VectorDbSearchResult> Value, long? Count);

// Contracts
public interface IOpenAiClient
{
    Task<EmbeddingResponse?> CreateEmbeddingAsync(string input, CancellationToken cancellationToken = default);
    Task<ChatCompletionResponse?> CreateChatCompletionAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
    Task<OpenAiItemResponse?> GetItemAsync(string id, CancellationToken cancellationToken = default);
}

public interface IVectorDbClient
{
    Task<IReadOnlyList<VectorDbSearchResult>> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken = default);
}

public interface IWeatherService
{
    Task<IReadOnlyList<WeatherForecast>> GetForecastAsync(CancellationToken cancellationToken = default);
}

public interface IRagOrchestrator
{
    Task<ChatOrchestrationResult> GenerateAnswerAsync(string question, CancellationToken cancellationToken = default);
}

public record ChatOrchestrationResult(string Answer, EmbeddingResponse? Embedding, IReadOnlyList<VectorDbSearchResult> RetrievedChunks, ChatCompletionResponse? Completion);

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
