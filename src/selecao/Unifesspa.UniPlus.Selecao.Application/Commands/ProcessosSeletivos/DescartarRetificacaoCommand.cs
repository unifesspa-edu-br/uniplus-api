namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Descarta a sessão editorial: o administrador abriu e desistiu (Story #861, ADR-0110).
/// </summary>
/// <remarks>
/// Não é plano de recuperação de dados — é <b>comando de negócio</b>. E é o que faz a
/// abertura de uma sessão deixar de ser um caminho sem volta.
/// </remarks>
public sealed record DescartarRetificacaoCommand(
    Guid ProcessoSeletivoId,
    PrecondicaoIfMatch Precondicao) : ICommand<Result>;
