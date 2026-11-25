namespace Rmg.Modules.Identity.Contracts;

// IAM endpoint payloads; success responses are Envelope-wrapped by the controllers (R-1).
public sealed record UserCreateRequest(string Email, string DisplayName, string Password, string[]? RoleCodes);

public sealed record SetUserRolesRequest(string[]? RoleCodes);

public sealed record UserDto(Guid Id, string Email, string DisplayName, bool IsActive, string[] Roles);

public sealed record RoleDto(Guid Id, string Code);
