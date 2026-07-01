namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) uma fase canônica pelo seu <c>Id</c>.</summary>
public sealed record RemoverFaseCanonicaCommand(Guid Id) : ICommand<Result>;
