namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da criação de versão; o caso de uso vive em <see cref="VersoesDoPerfil"/>.</summary>
public static class CriarVersaoDoPerfilCommandHandler
{
    public static Task<Result<EnvioDoDocumentoDto>> Handle(
        CriarVersaoDoPerfilCommand command, VersoesDoPerfil versoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(versoes);

        return versoes.CriarVersaoAsync(command.PerfilId, command.Dados, cancellationToken);
    }
}
