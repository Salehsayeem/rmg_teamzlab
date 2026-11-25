namespace Rmg.BuildingBlocks.Common;

// The 409 sibling of ConflictProblem with a stable per-aggregate code (e.g. FACILITY_REVISION_CONFLICT)
// for optimistic-concurrency (revision) collisions: services throw it when they catch a
// DbUpdateConcurrencyException for an expected-revision write.
public class RevisionConflictProblem(string Code, string? Detail = null)
    : RmgProblemException(Code, "The record was modified by another request.", 409, null, Detail);
