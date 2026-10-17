namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.FecharRetificacao"/> — o do atalho atômico
/// <b>menos o motivo</b>, que já foi declarado na abertura e vive no rascunho.
/// </summary>
/// <param name="Anexos">
/// Os anexos da nova versão. Ausente mantém os da versão vigente; uma lista substitui o conjunto inteiro — o anexo que continua o mesmo (mesmo documento e mesmo nome) mantém o endereço público, o trocado ou renomeado ganha outro —, e a lista vazia retira todos.
/// </param>
public sealed record FecharRetificacaoRequest(
    string? Numero,
    DateTimeOffset? PeriodoInscricaoInicio,
    DateTimeOffset? PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato,
    IReadOnlyList<AnexoDoEditalRequest>? Anexos = null);
