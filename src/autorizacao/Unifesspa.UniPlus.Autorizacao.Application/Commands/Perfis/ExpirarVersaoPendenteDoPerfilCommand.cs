namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Materializa o fim do prazo da versão pendente de um perfil; devolve se havia pendente vencida.</summary>
public sealed record ExpirarVersaoPendenteDoPerfilCommand(Guid PerfilId) : ICommand<Result<bool>>;
