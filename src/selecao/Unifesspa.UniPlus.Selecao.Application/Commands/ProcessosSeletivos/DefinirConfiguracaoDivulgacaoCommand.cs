namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Define (ou remove) a divulgação pública do processo (UNI-REQ-0050, issue #563).
/// </summary>
/// <remarks>
/// <see cref="CamposPublicos"/> nulo remove a configuração explícita — o processo volta ao
/// default minimizado (só o número de inscrição). Lista vazia é sempre recusada: não é o
/// mesmo pedido que remover a configuração. Este contrato de três estados é o que permite
/// desfazer uma ampliação anterior — inclusive removendo <c>nome_abreviado</c> durante uma
/// sessão de retificação.
/// </remarks>
public sealed record DefinirConfiguracaoDivulgacaoCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<string>? CamposPublicos,
    string? Justificativa,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
