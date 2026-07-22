namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.Publicar"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
public sealed record PublicarProcessoSeletivoRequest(
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato);
