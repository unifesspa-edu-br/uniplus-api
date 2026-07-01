namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) uma modalidade de concorrência pelo seu <c>Id</c>.</summary>
public sealed record RemoverModalidadeCommand(Guid Id) : ICommand<Result>;
