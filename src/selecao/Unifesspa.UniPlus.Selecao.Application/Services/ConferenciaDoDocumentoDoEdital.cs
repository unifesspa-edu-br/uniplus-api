namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// A conferência do documento que o ato do edital referencia — na publicação, na retificação e no
/// fechamento da retificação: existe, é do processo, está confirmado e é um PDF. É a mesma recusa
/// nos três, antes de qualquer escrita; o edital é divulgado como PDF.
/// </summary>
public static class ConferenciaDoDocumentoDoEdital
{
    public const string NaoEncontrado = "ProcessoSeletivo.DocumentoNaoEncontrado";
    public const string NaoConfirmado = "ProcessoSeletivo.DocumentoNaoConfirmado";
    public const string NaoEPdf = "ProcessoSeletivo.DocumentoDoEditalNaoEPdf";

    /// <param name="usoDoDocumento">Onde o documento é referenciado, para a mensagem: "na publicação", "na retificação"...</param>
    public static async Task<Result<DocumentoAPublicar>> ConferirAsync(
        IDocumentoAPublicarRepository repositorio,
        Guid processoSeletivoId,
        Guid documentoId,
        string usoDoDocumento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentException.ThrowIfNullOrWhiteSpace(usoDoDocumento);

        DocumentoAPublicar? documento = await repositorio.ObterPorIdAsync(documentoId, cancellationToken).ConfigureAwait(false);
        if (documento is null || documento.ProcessoSeletivoId != processoSeletivoId)
        {
            return Result<DocumentoAPublicar>.Failure(new DomainError(
                NaoEncontrado, $"Documento {documentoId} não encontrado ou não pertence a este processo."));
        }

        if (documento.Status != StatusArquivoEnviado.Confirmado)
        {
            return Result<DocumentoAPublicar>.Failure(new DomainError(
                NaoConfirmado, $"Somente um documento confirmado pode ser referenciado {usoDoDocumento}."));
        }

        return documento.Formato == FormatoDeDocumento.Pdf
            ? Result<DocumentoAPublicar>.Success(documento)
            : Result<DocumentoAPublicar>.Failure(new DomainError(NaoEPdf, "O documento do edital deve ser um PDF."));
    }
}
