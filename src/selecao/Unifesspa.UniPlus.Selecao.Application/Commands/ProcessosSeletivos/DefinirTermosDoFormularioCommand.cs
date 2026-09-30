namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Um termo que o formulário exige: o identificador da exigência, a ordem, o termo e a versão do
/// catálogo, a exibição (nula quando o termo sempre aparece) e a obrigatoriedade
/// (<c>SEMPRE</c>, <c>NUNCA</c> ou <c>QUANDO</c>, esta com predicado). Os predicados têm a forma
/// da pré-condição de fato coletado: OU de cláusulas, cada uma E de condições.
/// </summary>
public sealed record TermoExigidoInput(
    string Codigo,
    int Ordem,
    Guid TermoId,
    Guid VersaoId,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade);

/// <summary>
/// Substitui os termos exigidos pelo formulário de inscrição (UNI-REQ-0086). Editável em rascunho
/// e sob sessão de retificação, com o mesmo padrão dos demais <c>Definir*</c>.
/// </summary>
public sealed record DefinirTermosDoFormularioCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<TermoExigidoInput> Termos,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
