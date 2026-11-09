namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler do documento novo; o caso de uso vive em <see cref="DocumentosDasVersoes"/>.</summary>
public static class CriarDocumentoDaVersaoCommandHandler
{
    public static Task<Result<EnvioDoDocumentoDto>> Handle(
        CriarDocumentoDaVersaoCommand command, DocumentosDasVersoes documentos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(documentos);

        return documentos.NovoAsync(command.PerfilId, command.VersaoId, command.TamanhoDoDocumento, cancellationToken);
    }
}
