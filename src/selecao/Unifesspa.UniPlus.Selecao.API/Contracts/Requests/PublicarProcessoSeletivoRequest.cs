namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.Publicar"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <param name="Anexos">
/// Os anexos do edital, na ordem em que ele os apresenta, até 20. Ausente ou vazio quando o edital não tem anexo.
/// </param>
/// <param name="PeriodoInscricaoInicio">
/// Informado só quando o processo não tem fase do cronograma que colete inscrição, e então obrigatório.
/// Quando há essa fase, o período vem da janela dela e o valor informado é recusado.
/// </param>
/// <param name="PeriodoInscricaoFim">Idem <paramref name="PeriodoInscricaoInicio"/>.</param>
public sealed record PublicarProcessoSeletivoRequest(
    string? Numero,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato,
    IReadOnlyList<AnexoDoEditalRequest>? Anexos = null,
    DateTimeOffset? PeriodoInscricaoInicio = null,
    DateTimeOffset? PeriodoInscricaoFim = null);
