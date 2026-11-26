namespace Rmg.Modules.Organization.Contracts;

// org.facility read contract. Address mirrors the domain's nullable column (the seeded FAC-01 has none).
public sealed record FacilityDto(Guid Id, string Code, string Name, string Timezone, string? Address);
