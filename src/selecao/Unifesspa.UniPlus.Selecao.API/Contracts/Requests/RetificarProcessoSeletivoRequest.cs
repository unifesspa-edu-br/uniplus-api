namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.Retificar"/> — carrega só os
/// dados próprios da retificação. Omite <c>ProcessoSeletivoId</c> (vem da rota)
/// e não recebe id de Edital: o Edital sucedido é o vigente, resolvido no
/// servidor (a retificação endereça o agregado, não uma entidade interna).
/// </summary>
/// <param name="Anexos">
/// Os anexos da nova versão. Ausente mantém os da versão vigente; uma lista substitui o conjunto inteiro — o anexo que continua o mesmo (mesmo documento e mesmo nome) mantém o endereço público, o trocado ou renomeado ganha outro —, e a lista vazia retira todos.
/// </param>
public sealed record RetificarProcessoSeletivoRequest(
    string Motivo,
    string? Numero,
    DateTimeOffset? PeriodoInscricaoInicio,
    DateTimeOffset? PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAtoRequest Ato,
    IReadOnlyList<AnexoDoEditalRequest>? Anexos = null);
