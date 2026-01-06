using RAG_Challenge.Domain.Models.Rag;

namespace RAG_Challenge.Application.Helpers;

public static class RetryHelper
{
    public static async Task<Result<T>> ExecuteWithRetryAsync<T>(
        Func<Task<Result<T>>> action,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var result = Result<T>.Failure("Operation not executed");

        for (var i = 0; i <= maxRetries; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Result<T>.Failure("Operation cancelled");
            }

            result = await action();
            if (result.IsSuccess)
            {
                return result;
            }
        }

        return result;
    }
}