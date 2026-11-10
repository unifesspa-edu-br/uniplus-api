namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da desativação de perfil. Concessões existentes seguem como estão, e a versão pendente também: desativar
/// só impede versões e pedidos novos.
/// </summary>
public static class DesativarPerfilCommandHandler
{
    public static Task<Result> Handle(DesativarPerfilCommand command, AtivacaoDoPerfil ativacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ativacao);

        return ativacao.DesativarAsync(command.Id, command.Motivo, cancellationToken);
    }
}
