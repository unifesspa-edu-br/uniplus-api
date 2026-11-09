namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da URL de envio nova; o caso de uso vive em <see cref="DocumentosDasVersoes"/>.</summary>
public static class ReemitirEnvioDoDocumentoDaVersaoCommandHandler
{
    public static Task<Result<EnvioDoDocumentoDto>> Handle(
        ReemitirEnvioDoDocumentoDaVersaoCommand command, DocumentosDasVersoes documentos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(documentos);

        return documentos.ReemitirEnvioAsync(command.PerfilId, command.VersaoId, cancellationToken);
    }
}
