namespace RAG_Challenge.Domain.Models.Chat;

public record ChatChoice(ChatMessage Message, string? FinishReason);