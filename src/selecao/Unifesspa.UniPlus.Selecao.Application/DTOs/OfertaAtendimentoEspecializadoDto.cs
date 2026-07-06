namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura de uma condição ofertada (Story #758).</summary>
public sealed record OfertaCondicaoDto(Guid Id, Guid CondicaoOrigemId, string CondicaoCodigo, string CondicaoNome);

/// <summary>Projeção de leitura de um recurso ofertado (Story #758).</summary>
public sealed record OfertaRecursoDto(Guid Id, Guid RecursoOrigemId, string RecursoNome);

/// <summary>Projeção de leitura de um tipo de deficiência ofertado (Story #758, ADR-0067).</summary>
public sealed record OfertaTipoDeficienciaDto(Guid Id, Guid TipoDeficienciaOrigemId, string TipoDeficienciaNome);

/// <summary>
/// Projeção de leitura de <c>OfertaAtendimentoEspecializado</c> (Story #758,
/// CA-06).
/// </summary>
public sealed record OfertaAtendimentoEspecializadoDto(
    Guid Id,
    IReadOnlyList<OfertaCondicaoDto> Condicoes,
    IReadOnlyList<OfertaRecursoDto> Recursos,
    IReadOnlyList<OfertaTipoDeficienciaDto> TiposDeficiencia);
