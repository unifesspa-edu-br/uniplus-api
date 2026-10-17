namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Services;

/// <summary>
/// Handler de <see cref="IniciarEnvioDoDocumentoAPublicarCommand"/>: confere que o processo existe,
/// cria o registro pendente e devolve a URL pré-assinada de PUT, de validade curta
/// (<see cref="PrazosDoArquivoEnviado.Envio"/>).
/// </summary>
public static class IniciarEnvioDoDocumentoAPublicarCommandHandler
{
    public static async Task<Result<IniciarEnvioDoDocumentoAPublicarDto>> Handle(
        IniciarEnvioDoDocumentoAPublicarCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        if (!await processoSeletivoRepository.ExisteAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false))
        {
            return Result<IniciarEnvioDoDocumentoAPublicarDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado", "Processo Seletivo não encontrado."));
        }

        Result<DocumentoAPublicar> pendente = DocumentoAPublicar.IniciarPendente(
            command.ProcessoSeletivoId, command.NomeArquivo, command.TipoDeMidia, clock, PrazosDoArquivoEnviado.Envio);
        if (pendente.IsFailure)
        {
            return Result<IniciarEnvioDoDocumentoAPublicarDto>.ValidationFailure(pendente.Errors);
        }

        DocumentoAPublicar documento = pendente.Value!;

        // A URL sai antes de persistir: se o armazenamento falhar, nada é gravado, e o retry com a
        // mesma Idempotency-Key não deixa um pendente órfão nem cria outro.
        string urlUpload = await storage
            .GerarUrlUploadAsync(documento.ObjectKey, documento.TipoDeMidia, PrazosDoArquivoEnviado.Envio, cancellationToken)
            .ConfigureAwait(false);

        await documentoAPublicarRepository.AdicionarAsync(documento, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<IniciarEnvioDoDocumentoAPublicarDto>.Success(new IniciarEnvioDoDocumentoAPublicarDto(
            documento.Id, new Uri(urlUpload, UriKind.Absolute), documento.TipoDeMidia, documento.ExpiraEm));
    }
}
