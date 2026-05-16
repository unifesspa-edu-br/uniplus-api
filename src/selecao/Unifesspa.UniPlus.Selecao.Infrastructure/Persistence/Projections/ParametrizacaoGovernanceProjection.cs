namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Projections;

using Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Projection read-only desnormalizada (ADR-0057 §"Pattern 1", "Implementação")
/// que materializa o tuple atual <c>(item_id, item_type, proprietario,
/// areas_de_interesse)</c> para alta eficiência de leitura por
/// <c>Edital.Publicar()</c>. <strong>Skeleton — esta Story (#460) cria apenas
/// a definição.</strong>
/// </summary>
/// <remarks>
/// <para>
/// <strong>Lida em #462 (US-F4-04) <c>Edital.Publicar()</c> — não consumir
/// antes dessa Story mergear.</strong> A leitura efetiva (query SQL contra
/// a view <c>vw_catalog_area_visibility</c> per ADR-0060 + materialização
/// para insert em <c>EditalGovernanceSnapshot</c>) é responsabilidade da
/// próxima Story para respeitar "1 PR por Story" do Uni+.
/// </para>
/// <para>
/// Em V1 a projection é estática (record) — quando #462 ativar, vira keyless
/// entity type vinculada à view <c>selecao.vw_catalog_area_visibility</c>
/// que junta as 5 junctions área-scoped do banco Selecao (Modalidade,
/// LocalProva, TipoDocumento, ObrigatoriedadeLegal, etc.) com discriminador
/// <see cref="ItemType"/>. A coluna <see cref="AreasDeInteresseCodigos"/>
/// agrega o array Postgres via <c>ARRAY_AGG</c> filtrado por
/// <c>valid_to IS NULL</c>.
/// </para>
/// </remarks>
public sealed record ParametrizacaoGovernanceProjection(
    Guid ItemId,
    string ItemType,
    AreaCodigo? ProprietarioCodigo,
    IReadOnlyList<AreaCodigo> AreasDeInteresseCodigos);
