namespace Rmg.BuildingBlocks.Common;

// The 401 sibling of ForbiddenProblem — R-2 authentication failures. UNAUTHENTICATED covers
// missing/invalid tokens and missing tenant claims; login failures carry INVALID_CREDENTIALS.
public class UnauthorizedProblem(string? Detail = null, string Code = "UNAUTHENTICATED")
    : RmgProblemException(Code, "Unauthorized", 401, null, Detail);
