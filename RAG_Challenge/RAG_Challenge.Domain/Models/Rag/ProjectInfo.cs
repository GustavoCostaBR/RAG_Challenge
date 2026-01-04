using System.Collections.Frozen;

namespace RAG_Challenge.Domain.Models.Rag;

public static class Projects
{
    // TODO: it can possibly be recorded in a db so we can add more projects dynamically
    public static readonly Guid TeslaMotorsId = Guid.Parse("0a52b428-e00b-4f16-af14-98404f17fab7");

    private static readonly FrozenDictionary<Guid, string> Filters = new Dictionary<Guid, string>
    {
        [TeslaMotorsId] = "tesla_motors"
    }.ToFrozenDictionary();

    public static Result<string> ToFilterValue(Guid projectId)
    {
        return Filters.TryGetValue(projectId, out var filter)
            ? Result<string>.Success(filter)
            : Result<string>.Failure($"Unknown project ID: {projectId}");
    }
}