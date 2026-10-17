namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.Publicar"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <param name="Anexos">
/// Os anexos do edital, na ordem em que ele os apresenta, até 20. Ausente ou vazio quando o edital não tem anexo.
/// </param>
public sealed record PublicarProcessoSeletivoRequest(
    string? Numero,
    DateTimeOffset? PeriodoInscricaoInicio,
    DateTimeOffset? PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato,
    IReadOnlyList<AnexoDoEditalRequest>? Anexos = null);
