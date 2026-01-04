namespace RAG_Challenge.Domain.Models.Rag;

public enum StatusCode
{
    Ok,
    Error
}

public record Status(StatusCode Code, string? ErrorMessage = null)
{
    public static Status Ok() => new(StatusCode.Ok);
    public static Status Error(string message) => new(StatusCode.Error, message);
}

