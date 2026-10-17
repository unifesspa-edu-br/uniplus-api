namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Mappings;

using Services;

/// <summary>Handler de <see cref="ConfirmarEnvioDoDocumentoAPublicarCommand"/>.</summary>
public static class ConfirmarEnvioDoDocumentoAPublicarCommandHandler
{
    private static readonly ConfirmacaoDoArquivoEnviado.Recusas Recusas = new(
        ObjetoNaoEncontrado: new DomainError(
            DocumentoAPublicarErrorCodes.ObjetoNaoEncontrado,
            "O arquivo ainda não foi enviado ao armazenamento ou expirou antes da confirmação."),
        TamanhoExcedido: DocumentoAPublicar.TamanhoExcedido,
        StatusInvalido: new DomainError(
            DocumentoAPublicarErrorCodes.StatusInvalidoParaConfirmacao, "Somente um documento pendente pode ser confirmado."));

    public static async Task<Result<DocumentoAPublicarDto>> Handle(
        ConfirmarEnvioDoDocumentoAPublicarCommand command,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        DocumentoAPublicar? documento = await documentoAPublicarRepository
            .ObterPorIdAsync(command.DocumentoAPublicarId, cancellationToken)
            .ConfigureAwait(false);
        if (documento is null || documento.ProcessoSeletivoId != command.ProcessoSeletivoId)
        {
            return Result<DocumentoAPublicarDto>.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.NaoEncontrado, "Documento não encontrado neste Processo Seletivo."));
        }

        Result confirmacao = await ConfirmacaoDoArquivoEnviado
            .ConfirmarAsync(documento, DocumentoAPublicar.TamanhoMaximoBytes, Recusas, documentoAPublicarRepository, storage, clock, cancellationToken)
            .ConfigureAwait(false);
        if (confirmacao.IsFailure)
        {
            return Result<DocumentoAPublicarDto>.Failure(confirmacao.Error!);
        }

        documentoAPublicarRepository.Atualizar(documento);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<DocumentoAPublicarDto>.Success(DocumentoAPublicarMapping.ToDto(documento));
    }
}
