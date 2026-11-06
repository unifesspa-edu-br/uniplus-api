namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Reativa um perfil de acesso.</summary>
public sealed record ReativarPerfilCommand(Guid Id) : ICommand<Result>;
