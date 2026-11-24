namespace Rmg.BuildingBlocks.Common;

// R-1: domain errors carry a stable code and are rendered as RFC 9457 problem+json.
public class RmgProblemException(
    string Code,
    string Title,
    int Status,
    Dictionary<string, string[]>? Errors = null,
    string? Detail = null) : Exception(Detail ?? Title)
{
    public string Code { get; } = Code;
    public string Title { get; } = Title;
    public int Status { get; } = Status;
    public Dictionary<string, string[]>? Errors { get; } = Errors;
    public string? Detail { get; } = Detail;
}

public class ValidationProblem(Dictionary<string, string[]>? Errors = null)
    : RmgProblemException("VALIDATION_FAILED", "One or more validation errors occurred.", 400, Errors);

public class ConflictProblem(string? Detail = null)
    : RmgProblemException("CONFLICT", "Conflict.", 409, null, Detail);

public class NotFoundProblem(string? Detail = null)
    : RmgProblemException("NOT_FOUND", "Not Found", 404, null, Detail);

public class ForbiddenProblem(string? Detail = null)
    : RmgProblemException("FORBIDDEN", "Forbidden", 403, null, Detail);

public class BusinessRuleProblem(string Code, string? Detail = null)
    : RmgProblemException(Code, "Business rule violated.", 422, null, Detail);
