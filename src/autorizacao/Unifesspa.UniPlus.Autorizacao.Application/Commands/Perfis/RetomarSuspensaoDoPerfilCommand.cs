namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Decide uma etapa da retomada da suspensão do perfil, com motivo; a permissão da etapa se confere no handler.</summary>
public sealed record RetomarSuspensaoDoPerfilCommand(Guid PerfilId, string? Etapa, string? Motivo) : ICommand<Result<RetomadaDaSuspensaoDto>>;
