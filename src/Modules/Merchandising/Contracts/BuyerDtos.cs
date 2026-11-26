namespace Rmg.Modules.Merchandising.Contracts;

// ORD endpoint payloads; success responses are Envelope-wrapped by the controller (R-1).
public sealed record BuyerCreateRequest(string Code, string Name, string Country, string Currency, ContactInput[]? Contacts);

public sealed record ContactInput(string Name, string? Email, string? Phone);

public sealed record BuyerContactDto(string Name, string? Email, string? Phone);

// Contact email/phone are null when the caller lacks ord.buyer.manage (stripped server-side).
public sealed record BuyerDto(Guid Id, string Code, string Name, string Country, string Currency, string Status, int Revision, BuyerContactDto[] Contacts);
