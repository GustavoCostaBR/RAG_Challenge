namespace RAG_Challenge.Domain.Models.Rag;

public class Result<T>
{
    public T? Value { get; }
    public Status Status { get; }

    public bool IsSuccess => Status.Code == StatusCode.Ok;

    private Result(T? value, Status status)
    {
        Value = value;
        Status = status;
    }

    public static Result<T> Success(T value) => new(value, Status.Ok());
    public static Result<T> Failure(Status status) => new(default, status);
    public static Result<T> Failure(string errorMessage) => new(default, Status.Error(errorMessage));

    public void Deconstruct(out T? value, out Status status)
    {
        value = Value;
        status = Status;
    }
}