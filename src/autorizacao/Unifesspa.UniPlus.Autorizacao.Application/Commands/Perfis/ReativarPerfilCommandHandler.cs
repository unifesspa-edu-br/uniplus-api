namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da reativação de perfil. A operação trava o perfil como as demais.</summary>
public static class ReativarPerfilCommandHandler
{
    public static Task<Result> Handle(ReativarPerfilCommand command, OperacaoSobreOPerfil operacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(operacao);

        return operacao.ExecutarAsync(command.Id, static perfil => perfil.Reativar(), cancellationToken);
    }
}
