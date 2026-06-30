namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) um recurso de acessibilidade pelo seu <c>Id</c>.</summary>
public sealed record RemoverRecursoAcessibilidadeCommand(Guid Id) : ICommand<Result>;
