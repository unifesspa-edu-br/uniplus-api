namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da desativação de perfil. Concessões existentes seguem como estão, e a versão pendente também: desativar
/// só impede versões e pedidos novos. A operação trava o perfil como as demais.
/// </summary>
public static class DesativarPerfilCommandHandler
{
    public static Task<Result> Handle(DesativarPerfilCommand command, OperacaoSobreOPerfil operacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(operacao);

        return operacao.ExecutarAsync(command.Id, static perfil => perfil.Desativar(), cancellationToken);
    }
}
