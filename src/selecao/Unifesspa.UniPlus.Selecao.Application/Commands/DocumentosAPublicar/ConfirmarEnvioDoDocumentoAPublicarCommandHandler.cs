namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Mappings;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler de <see cref="ConfirmarEnvioDoDocumentoAPublicarCommand"/>. Bloqueia o dono antes de qualquer leitura do
/// documento: a ordem dos bloqueios é a do rascunho, depois a do documento, a mesma de quem exclui ou publica o rascunho.
/// </summary>
public static class ConfirmarEnvioDoDocumentoAPublicarCommandHandler
{
    private static readonly ConfirmacaoDoArquivoEnviado.Recusas Recusas = new(
        ObjetoNaoEncontrado: new DomainError(
            DocumentoAPublicarErrorCodes.ObjetoNaoEncontrado,
            "O arquivo ainda não foi enviado ao armazenamento ou expirou antes da confirmação."),
        TamanhoExcedido: DocumentoAPublicar.TamanhoExcedido,
        StatusInvalido: new DomainError(
            DocumentoAPublicarErrorCodes.StatusInvalidoParaConfirmacao, "Somente um documento pendente pode ser confirmado."));

    private static readonly DomainError DocumentoNaoEncontrado = new(
        DocumentoAPublicarErrorCodes.NaoEncontrado, "Documento não encontrado neste Processo Seletivo.");

    public static async Task<Result<DocumentoAPublicarDto>> Handle(
        ConfirmarEnvioDoDocumentoAPublicarCommand command,
        BloqueioCompartilhadoDoProcessoSeletivo bloqueio,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(bloqueio);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        return await bloqueio.BloquearSeExisteAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false)
            ? await ConfirmarAsync(command, documentoAPublicarRepository, storage, unitOfWork, clock, cancellationToken).ConfigureAwait(false)
            : Result<DocumentoAPublicarDto>.Failure(AcessoAoProcesso.NaoEncontrado(command.ProcessoSeletivoId));
    }

    private static async Task<Result<DocumentoAPublicarDto>> ConfirmarAsync(
        ConfirmarEnvioDoDocumentoAPublicarCommand command,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        DocumentoAPublicar? documento = await documentoAPublicarRepository
            .ObterPorIdAsync(command.DocumentoAPublicarId, cancellationToken)
            .ConfigureAwait(false);

        return documento is null || documento.ProcessoSeletivoId != command.ProcessoSeletivoId
            ? Result<DocumentoAPublicarDto>.Failure(DocumentoNaoEncontrado)
            : await ConfirmarEGravarAsync(documento, documentoAPublicarRepository, storage, unitOfWork, clock, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<DocumentoAPublicarDto>> ConfirmarEGravarAsync(
        DocumentoAPublicar documento,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
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
