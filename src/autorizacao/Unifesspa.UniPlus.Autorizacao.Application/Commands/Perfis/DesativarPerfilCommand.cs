namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Desativa um perfil de acesso, com motivo. Não revoga concessão alguma.</summary>
public sealed record DesativarPerfilCommand(Guid Id, string? Motivo) : ICommand<Result>;
