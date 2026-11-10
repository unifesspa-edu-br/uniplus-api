namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da reativação de perfil.</summary>
public static class ReativarPerfilCommandHandler
{
    public static Task<Result> Handle(ReativarPerfilCommand command, AtivacaoDoPerfil ativacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ativacao);

        return ativacao.ReativarAsync(command.Id, command.Motivo, cancellationToken);
    }
}
