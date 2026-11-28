namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// A conferência do documento que o ato do edital referencia — na publicação e na
/// retificação: existe, é do processo, está confirmado e foi enviado como edital — o
/// que, pela finalidade, já o faz PDF. É a mesma recusa nas duas, antes de qualquer escrita.
/// </summary>
public static class ConferenciaDoDocumentoDoEdital
{
    public const string NaoEncontrado = "ProcessoSeletivo.DocumentoNaoEncontrado";
    public const string NaoConfirmado = "ProcessoSeletivo.DocumentoNaoConfirmado";
    public const string FinalidadeIncompativel = "ProcessoSeletivo.DocumentoNaoEnviadoComoEdital";
    public const string CampoDoDocumento = "documentoEditalId";

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

        return documento is null || documento.ProcessoSeletivoId != processoSeletivoId
            ? Recusa(NaoEncontrado, "O documento do edital deve ser um dos documentos deste processo.")
            : ConferirASituacao(documento, usoDoDocumento);
    }

    private static Result<DocumentoAPublicar> ConferirASituacao(DocumentoAPublicar documento, string usoDoDocumento)
    {
        if (documento.Situacao != StatusArquivoEnviado.Confirmado)
        {
            return Recusa(NaoConfirmado, $"Somente um documento confirmado pode ser referenciado {usoDoDocumento}.");
        }

        return documento.Finalidade == FinalidadeDoDocumento.DocumentoDoEdital
            ? Result<DocumentoAPublicar>.Success(documento)
            : Recusa(FinalidadeIncompativel, "Somente um documento enviado como edital pode ser o documento do edital.");
    }

    // Toda recusa do documento aponta o campo do pedido em que se corrige, para sair na lista junto das recusas do tipo do ato.
    private static Result<DocumentoAPublicar> Recusa(string codigo, string mensagem) =>
        Result<DocumentoAPublicar>.ValidationFailure([new FieldError(CampoDoDocumento, new DomainError(codigo, mensagem))]);
}
