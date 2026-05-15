namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// DTO read-only de <c>AreaOrganizacional</c> para consumo cross-módulo via
/// <see cref="IAreaOrganizacionalReader"/> (ADR-0055/0056). Não carrega
/// <c>_links</c> HATEOAS — é representação interna entre módulos; HATEOAS é
/// responsabilidade do boundary HTTP de quem expõe.
/// </summary>
/// <param name="Id">Identificador único da área (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Código strongly-typed da área (CEPS, CRCA, PROEG, …).</param>
/// <param name="Nome">Nome de exibição da área.</param>
/// <param name="Tipo">
/// Classificação organizacional (<c>ProReitoria</c>, <c>Centro</c>, <c>Coordenadoria</c>,
/// <c>Plataforma</c>, <c>Outra</c>). Expresso como string para desacoplar o
/// consumidor cross-módulo do enum interno do bounded context.
/// </param>
/// <param name="Descricao">Descrição operacional da área.</param>
/// <param name="AdrReferenceCode">
/// Identificador da ADR que justifica a presença desta área no roster fechado
/// (ADR-0055 §"Invariante de roster fechado").
/// </param>
/// <param name="CreatedAt">Instante de criação (UTC).</param>
public sealed record AreaOrganizacionalView(
    Guid Id,
    AreaCodigo Codigo,
    string Nome,
    string Tipo,
    string Descricao,
    string AdrReferenceCode,
    DateTimeOffset CreatedAt);
