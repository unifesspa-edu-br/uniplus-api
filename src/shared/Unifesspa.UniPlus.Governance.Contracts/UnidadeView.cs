namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// DTO read-only de <c>Unidade</c> para consumo cross-módulo via
/// <see cref="IUnidadeReader"/> (ADR-0056). Expõe apenas os campos
/// necessários para vinculação e exibição — sem dados de auditoria interna
/// nem histórico de identificadores.
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Sigla">Sigla corrente da Unidade (uppercase).</param>
/// <param name="Slug">Identificador kebab-case da Unidade.</param>
/// <param name="Nome">Nome formal da Unidade.</param>
/// <param name="Alias">Nome popular de agrupamento, ou <see langword="null"/> se não informado.</param>
/// <param name="Tipo">Classificação organizacional como string (desacoplado do enum interno).</param>
/// <param name="UnidadeAcademica">Indica se é uma unidade acadêmica.</param>
/// <param name="UnidadeSuperiorId">Id da Unidade superior, ou <see langword="null"/> para a raiz.</param>
/// <param name="CidadeCodigoIbge">Código IBGE (7 dígitos) da cidade da Unidade, ou <see langword="null"/> se não cadastrada (issue #1114) — all-or-nothing com <see cref="CidadeNome"/>/<see cref="CidadeUf"/>.</param>
/// <param name="CidadeNome">Nome de exibição da cidade (display cache), ou <see langword="null"/> se não cadastrada.</param>
/// <param name="CidadeUf">UF da cidade (display cache), ou <see langword="null"/> se não cadastrada.</param>
public sealed record UnidadeView(
    Guid Id,
    string Sigla,
    string Slug,
    string Nome,
    string? Alias,
    string Tipo,
    bool UnidadeAcademica,
    Guid? UnidadeSuperiorId,
    string? CidadeCodigoIbge = null,
    string? CidadeNome = null,
    string? CidadeUf = null);
