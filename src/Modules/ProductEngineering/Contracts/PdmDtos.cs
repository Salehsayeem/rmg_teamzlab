namespace Rmg.Modules.ProductEngineering.Contracts;

// pdm.uom read contract — global reference data (no tenant fields).
public sealed record UomDto(string Code, string Name);

// pdm.material read/create contract; Category mirrors the nullable column.
public sealed record MaterialDto(Guid Id, string Code, string Name, string? Category, string BaseUomCode);

public sealed record MaterialCreateRequest(string Code, string Name, string? Category, string BaseUomCode);

// pdm.style read/create contract (Task 12) — detail carries versions with their variant matrix.
public sealed record StyleVariantInput(string ColorCode, string SizeCode);
public sealed record StyleCreateRequest(string Code, string Name, string? Season, string? Category, Guid BuyerId, StyleVariantInput[]? Variants);
public sealed record StyleVariantDto(Guid Id, string ColorCode, string SizeCode);
public sealed record StyleVersionDto(Guid Id, int Version, string Status, DateTime? ApprovedAtUtc, StyleVariantDto[] Variants);
public sealed record StyleDto(Guid Id, string Code, string Name, string? Season, string? Category, Guid BuyerId, string Status, int Revision, StyleVersionDto[] Versions);

// pdm.bom read/create contract (Task 13) — detail carries the line grid.
public sealed record BomCreateRequest(Guid StyleVersionId);
public sealed record BomLineAddRequest(Guid MaterialId, decimal QtyPerUnit, string UomCode, decimal? WastePct);
public sealed record BomLineDto(Guid Id, Guid MaterialId, decimal QtyPerUnit, string UomCode, decimal WastePct);
public sealed record BomDto(Guid Id, Guid StyleVersionId, string Status, int Revision, BomLineDto[] Lines);
