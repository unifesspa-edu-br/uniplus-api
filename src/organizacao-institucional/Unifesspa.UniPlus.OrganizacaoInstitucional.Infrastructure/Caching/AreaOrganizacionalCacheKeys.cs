namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching;

/// <summary>
/// Chaves canônicas do Redis usadas pelo reader e pelo cache invalidator.
/// Internas ao módulo — Application/Domain não conhecem.
/// </summary>
internal static class AreaOrganizacionalCacheKeys
{
    /// <summary>
    /// Snapshot completo da lista de áreas ativas. Chave global (não por-caller)
    /// per ADR-0057 Pattern 4 — filtro de visibilidade não se aplica a
    /// <see cref="Governance.Contracts.AreaOrganizacionalView"/> (áreas são públicas).
    /// </summary>
    public const string TodasAsAreasAtivas = "organizacao:areas-organizacionais:ativas";

    /// <summary>
    /// Lease para coordenar populadores concorrentes em cache miss (stampede protection).
    /// </summary>
    public const string LeaseTodasAsAreasAtivas = "organizacao:areas-organizacionais:ativas:lease";
}
