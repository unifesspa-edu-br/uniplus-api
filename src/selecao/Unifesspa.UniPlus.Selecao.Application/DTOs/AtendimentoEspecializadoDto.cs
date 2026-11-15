namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura de uma condição ofertada (Story #758).</summary>
public sealed record CondicaoOfertadaDto(Guid Id, Guid CondicaoOrigemId, string CondicaoCodigo, string CondicaoNome);

/// <summary>Projeção de leitura de um recurso ofertado (Story #758).</summary>
public sealed record RecursoOfertadoDto(Guid Id, Guid RecursoOrigemId, string RecursoCodigo, string RecursoNome);

/// <summary>Projeção de leitura de um tipo de deficiência ofertado (Story #758, ADR-0067).</summary>
public sealed record DeficienciaAtendidaDto(Guid Id, Guid TipoDeficienciaOrigemId, string TipoDeficienciaNome);

/// <summary>
/// Projeção de leitura de <c>AtendimentoEspecializado</c> (Story #758,
/// CA-06).
/// </summary>
public sealed record AtendimentoEspecializadoDto(
    Guid Id,
    IReadOnlyList<CondicaoOfertadaDto> Condicoes,
    IReadOnlyList<RecursoOfertadoDto> Recursos,
    IReadOnlyList<DeficienciaAtendidaDto> TiposDeficiencia);
