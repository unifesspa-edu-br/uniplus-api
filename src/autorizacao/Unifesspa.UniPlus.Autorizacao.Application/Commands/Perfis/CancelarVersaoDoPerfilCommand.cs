namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Cancela a versão pendente do perfil. Qualquer pessoa que mantém perfis pode cancelá-la.</summary>
public sealed record CancelarVersaoDoPerfilCommand(Guid PerfilId, Guid VersaoId) : ICommand<Result>;
