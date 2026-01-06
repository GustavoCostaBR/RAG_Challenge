using System.Text.Json;
using RAG_Challenge.Domain.Models.Rag;

namespace RAG_Challenge.Application.Helpers;

public static class ModelResponseParser
{
    public static Result<(string Answer, bool HandoverToHumanNeeded)> ParseModelResponse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<(string, bool)>.Failure("Model response is empty");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<(string, bool)>.Failure("Model response is not a JSON object");
            }

            if (!root.TryGetProperty("answer", out var a) || a.ValueKind != JsonValueKind.String)
            {
                return Result<(string, bool)>.Failure("JSON response missing 'answer' property or it is not a string");
            }

            var ans = a.GetString() ?? string.Empty;

            var handoverToHuman = false;
            if (root.TryGetProperty("handoverToHumanNeeded", out var h))
            {
                if (h.ValueKind == JsonValueKind.True) handoverToHuman = true;
            }

            return Result<(string, bool)>.Success((ans, handoverToHuman));
        }
        catch (JsonException)
        {
            return Result<(string, bool)>.Failure("Failed to parse model response as JSON");
        }
        catch (Exception ex)
        {
            return Result<(string, bool)>.Failure($"Unexpected error parsing model response: {ex.Message}");
        }
    }
}