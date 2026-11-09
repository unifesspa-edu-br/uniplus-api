namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da criação de perfil; o caso de uso vive em <see cref="VersoesDoPerfil"/>.</summary>
public static class CriarPerfilCommandHandler
{
    public static Task<Result<EnvioDoDocumentoDto>> Handle(
        CriarPerfilCommand command, VersoesDoPerfil versoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(versoes);

        return versoes.CriarPerfilAsync(command.Dados, cancellationToken);
    }
}
