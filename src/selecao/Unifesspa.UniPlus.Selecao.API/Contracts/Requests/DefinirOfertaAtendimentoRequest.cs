namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoEtapasEVagasController.DefinirOfertaAtendimento"/>
/// — omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record DefinirOfertaAtendimentoRequest(
    IReadOnlyList<Guid> CondicaoIds,
    IReadOnlyList<Guid> RecursoIds,
    IReadOnlyList<Guid> TipoDeficienciaIds);
