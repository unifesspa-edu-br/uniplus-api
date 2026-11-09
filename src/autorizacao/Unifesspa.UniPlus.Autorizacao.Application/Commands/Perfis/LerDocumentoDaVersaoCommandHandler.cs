namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da leitura do documento; o caso de uso vive em <see cref="DocumentosDasVersoes"/>.</summary>
public static class LerDocumentoDaVersaoCommandHandler
{
    public static Task<Result<LeituraDoDocumentoDaVersaoDto>> Handle(
        LerDocumentoDaVersaoCommand command,
        DocumentosDasVersoes documentos,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(documentos);

        return documentos.LerAsync(command.PerfilId, command.VersaoId, verificador, cancellationToken);
    }
}
