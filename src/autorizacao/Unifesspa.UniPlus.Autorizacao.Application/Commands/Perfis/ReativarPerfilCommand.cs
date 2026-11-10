namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Reativa um perfil de acesso, com motivo, por outra pessoa que não quem o desativou.</summary>
public sealed record ReativarPerfilCommand(Guid Id, string? Motivo) : ICommand<Result>;
