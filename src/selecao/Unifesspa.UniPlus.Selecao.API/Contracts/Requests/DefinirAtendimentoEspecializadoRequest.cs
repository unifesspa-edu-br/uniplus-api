namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoEtapasEVagasController.DefinirAtendimentoEspecializado"/>
/// — omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record DefinirAtendimentoEspecializadoRequest(
    IReadOnlyList<Guid> CondicaoIds,
    IReadOnlyList<Guid> RecursoIds,
    IReadOnlyList<Guid> TipoDeficienciaIds);
