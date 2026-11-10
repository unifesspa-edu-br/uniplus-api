namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Suspende um perfil de acesso, com motivo; a permissão se confere no handler.</summary>
public sealed record SuspenderPerfilCommand(Guid PerfilId, string? Motivo) : ICommand<Result>;
