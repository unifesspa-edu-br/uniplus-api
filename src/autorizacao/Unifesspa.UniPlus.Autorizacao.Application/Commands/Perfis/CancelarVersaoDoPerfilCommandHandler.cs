namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler do cancelamento de versão; o caso de uso vive em <see cref="VersoesDoPerfil"/>.</summary>
public static class CancelarVersaoDoPerfilCommandHandler
{
    public static Task<Result> Handle(
        CancelarVersaoDoPerfilCommand command, VersoesDoPerfil versoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(versoes);

        return versoes.CancelarVersaoAsync(command.PerfilId, command.VersaoId, cancellationToken);
    }
}
