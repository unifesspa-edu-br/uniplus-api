namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Reativa um perfil de acesso.</summary>
public sealed record AtivarPerfilCommand(Guid Id) : ICommand<Result>;
