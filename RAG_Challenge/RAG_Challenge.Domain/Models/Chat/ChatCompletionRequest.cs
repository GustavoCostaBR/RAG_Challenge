namespace RAG_Challenge.Domain.Models.Chat;

public record ChatCompletionRequest(string Model, IReadOnlyList<ChatMessage> Messages, double Temperature = 0);
