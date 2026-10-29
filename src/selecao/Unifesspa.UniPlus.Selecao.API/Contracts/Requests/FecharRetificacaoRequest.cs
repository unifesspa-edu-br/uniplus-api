namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.FecharRetificacao"/> — o do atalho atômico
/// <b>menos o motivo</b>, que já foi declarado na abertura e vive no rascunho.
/// </summary>
/// <param name="Anexos">
/// Os anexos da nova versão. Ausente mantém os da versão vigente; uma lista substitui o conjunto
/// inteiro, e a lista vazia retira todos. O anexo que continua o mesmo (mesmo documento e mesmo
/// nome) mantém a identidade; o trocado ou renomeado ganha outra. Cada versão publica os anexos nos
/// seus próprios endereços, e a versão anterior conserva os dela.
/// </param>
/// <param name="PeriodoInscricaoInicio">
/// Informado só quando o processo não tem fase do cronograma que colete inscrição, e então obrigatório.
/// Quando há essa fase, o período vem da janela dela e o valor informado é recusado.
/// </param>
/// <param name="PeriodoInscricaoFim">Idem <paramref name="PeriodoInscricaoInicio"/>.</param>
public sealed record FecharRetificacaoRequest(
    string? Numero,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato,
    IReadOnlyList<AnexoDoEditalRequest>? Anexos = null,
    DateTimeOffset? PeriodoInscricaoInicio = null,
    DateTimeOffset? PeriodoInscricaoFim = null);
