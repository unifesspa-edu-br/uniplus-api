namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) uma condição de atendimento especializado pelo seu <c>Id</c>.</summary>
public sealed record RemoverCondicaoAtendimentoCommand(Guid Id) : ICommand<Result>;
