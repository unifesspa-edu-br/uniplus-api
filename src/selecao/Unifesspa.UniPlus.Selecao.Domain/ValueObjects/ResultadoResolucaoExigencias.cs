namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Enums;

/// <summary>
/// Veredicto de <see cref="Services.ResolvedorExigenciasDocumentais"/> sobre UMA
/// <see cref="Entities.DocumentoExigido"/> congelada, para um candidato (Story #554, PR-e).
/// </summary>
/// <param name="ExigenciaId">O <c>exigenciaId</c> congelado (<c>DocumentoExigido.Id</c>, CA-09) — a chave de correlação.</param>
/// <param name="Status">Satisfeita, Pendente ou NaoAplicavel — nunca o sentinela <see cref="StatusResolucaoExigencia.Nenhuma"/>.</param>
/// <param name="ApresentacaoId">
/// A apresentação que satisfaz a exigência, quando <see cref="Status"/> é
/// <see cref="StatusResolucaoExigencia.Satisfeita"/> — a PRÓPRIA apresentação da exigência,
/// ou a de uma exigência-irmã do mesmo <see cref="Entities.DocumentoExigido.GrupoSatisfacaoId"/>
/// quando a satisfação vem do grupo. <see langword="null"/> nos demais status.
/// </param>
public sealed record ExigenciaResolvida(Guid ExigenciaId, StatusResolucaoExigencia Status, Guid? ApresentacaoId);

/// <summary>
/// O resultado agregado de <see cref="Services.ResolvedorExigenciasDocumentais.Resolver"/> —
/// uma <see cref="ExigenciaResolvida"/> por item de <see cref="BlocoExigenciasCongelado.Exigencias"/>,
/// nunca menos (ADR-0076: um resultado vazio nunca substitui um erro nomeado).
/// </summary>
public sealed record ResultadoResolucaoExigencias(IReadOnlyList<ExigenciaResolvida> Exigencias);
