namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.Retificar"/> — carrega só os
/// dados próprios da retificação. Omite <c>ProcessoSeletivoId</c> (vem da rota)
/// e não recebe id de Edital: o Edital sucedido é o vigente, resolvido no
/// servidor (a retificação endereça o agregado, não uma entidade interna).
/// </summary>
public sealed record RetificarProcessoSeletivoRequest(
    string Motivo,
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato);
