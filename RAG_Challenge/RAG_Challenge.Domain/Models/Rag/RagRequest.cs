using RAG_Challenge.Domain.Models.Chat;

namespace RAG_Challenge.Domain.Models.Rag;

public record RagRequest(string Question, IReadOnlyList<ChatMessage> History, Guid? ProjectId = null);
