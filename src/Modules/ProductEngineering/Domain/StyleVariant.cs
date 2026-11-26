using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.ProductEngineering.Domain;

// pdm.style_variant — one color × size cell of a version's variant matrix; uniqueness per
// (tenant_id, style_version_id, color_code, size_code) is both a service check and a unique index.
public class StyleVariant : TenantEntity
{
    public Guid StyleVersionId { get; set; }
    public string ColorCode { get; set; } = null!;
    public string SizeCode { get; set; } = null!;
}
