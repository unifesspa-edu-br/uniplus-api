namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Fecha a sessão editorial (Story #862, ADR-0110): congela a versão N+1 <b>com a
/// configuração editada</b> e registra o ato.
/// </summary>
/// <remarks>
/// <b>Não recebe o motivo.</b> Ele foi declarado na abertura, normalizado uma única vez e
/// vive no rascunho — recebê-lo de novo aqui abriria a porta para o motivo do ato divergir
/// do que a sessão registrou. O resto do corpo é o mesmo do atalho atômico: os dados do
/// edital e o ato que o operador declara.
/// </remarks>
/// <param name="Anexos">
/// Os anexos da nova versão. Nulo mantém os da vigente; uma lista substitui o conjunto inteiro, e a
/// lista vazia os retira todos.
/// </param>
public sealed record FecharRetificacaoCommand(
    Guid ProcessoSeletivoId,
    string? Numero,
    DateTimeOffset? PeriodoInscricaoInicio,
    DateTimeOffset? PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAto Ato,
    PrecondicaoIfMatch Precondicao,
    IReadOnlyList<AnexoSolicitadoDoEdital>? Anexos = null) : ICommand<Result>;
