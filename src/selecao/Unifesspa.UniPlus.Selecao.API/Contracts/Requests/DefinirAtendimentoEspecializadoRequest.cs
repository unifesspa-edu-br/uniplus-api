namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoEtapasEVagasController.DefinirAtendimentoEspecializado"/>
/// — omite <c>ProcessoSeletivoId</c> (vem da rota). As três listas são obrigatórias: ausente ou nula é recusada
/// (422), e a lista vazia declara que nada é ofertado naquela dimensão.
/// </summary>
public sealed record DefinirAtendimentoEspecializadoRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver nenhuma.")]
    IReadOnlyList<Guid>? CondicaoIds,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver nenhum.")]
    IReadOnlyList<Guid>? RecursoIds,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver nenhum.")]
    IReadOnlyList<Guid>? TipoDeficienciaIds);
