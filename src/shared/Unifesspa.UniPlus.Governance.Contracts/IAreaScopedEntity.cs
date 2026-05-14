namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Marca entidades de domínio que carregam governança por área organizacional
/// (ADR-0057). O <see cref="Proprietario"/> identifica a área que pode editar
/// o item; quando <see langword="null"/>, o item é global e gerido apenas pela
/// plataforma (CTIC).
/// </summary>
/// <remarks>
/// Vive em <c>Governance.Contracts</c> (dentro de <c>src/shared/</c>) — e não em
/// <c>Infrastructure.Core</c> — porque as entidades área-scoped de
/// <c>Domain</c> (Modalidade, TipoDocumento, etc.) a implementam, e a regra de
/// dependência da Clean Architecture proíbe <c>Domain</c> de referenciar
/// <c>Infrastructure</c>. O <c>AreaScopedAuthorizationHandler</c> em
/// <c>Infrastructure.Core</c> consome este marcador como recurso da
/// autorização baseada em recurso.
/// </remarks>
public interface IAreaScopedEntity
{
    /// <summary>
    /// Área organizacional dona do item (autoridade de escrita), ou
    /// <see langword="null"/> para itens globais geridos apenas pela plataforma.
    /// </summary>
    AreaCodigo? Proprietario { get; }
}
