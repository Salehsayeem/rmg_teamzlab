namespace Rmg.Modules.ProductEngineering.Contracts;

// pdm.uom read contract — global reference data (no tenant fields).
public sealed record UomDto(string Code, string Name);

// pdm.material read/create contract; Category mirrors the nullable column.
public sealed record MaterialDto(Guid Id, string Code, string Name, string? Category, string BaseUomCode);

public sealed record MaterialCreateRequest(string Code, string Name, string? Category, string BaseUomCode);
