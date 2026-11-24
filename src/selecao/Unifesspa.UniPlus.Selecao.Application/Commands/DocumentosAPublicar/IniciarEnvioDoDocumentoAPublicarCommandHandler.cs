namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using DTOs;

using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler de <see cref="IniciarEnvioDoDocumentoAPublicarCommand"/>: confere que há dono — o rascunho ou o processo —,
/// cria o registro pendente e devolve a URL pré-assinada de PUT, de validade curta
/// (<see cref="PrazosDoArquivoEnviado.Envio"/>). Não bloqueia o dono: o pendente criado em corrida com a exclusão do
/// rascunho fica sem dono e sai quando vence, pela rotina dos pendentes vencidos.
/// </summary>
public static class IniciarEnvioDoDocumentoAPublicarCommandHandler
{
    public static async Task<Result<IniciarEnvioDoDocumentoAPublicarDto>> Handle(
        IniciarEnvioDoDocumentoAPublicarCommand command,
        IUnidadeDonaDoProcesso donos,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(donos);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        return await donos.DoProcessoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false) is null
            ? Result<IniciarEnvioDoDocumentoAPublicarDto>.Failure(AcessoAoProcesso.NaoEncontrado(command.ProcessoSeletivoId))
            : await IniciarAsync(command, documentoAPublicarRepository, storage, unitOfWork, clock, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IniciarEnvioDoDocumentoAPublicarDto>> IniciarAsync(
        IniciarEnvioDoDocumentoAPublicarCommand command,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        Result<DocumentoAPublicar> pendente = DocumentoAPublicar.IniciarPendente(
            command.ProcessoSeletivoId, FinalidadeDeclarada(command.Finalidade), command.NomeArquivo, command.TipoDeMidia, clock, PrazosDoArquivoEnviado.Envio);

        return pendente.IsFailure
            ? Result<IniciarEnvioDoDocumentoAPublicarDto>.ValidationFailure(pendente.Errors)
            : await GravarPendenteAsync(pendente.Value!, documentoAPublicarRepository, storage, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IniciarEnvioDoDocumentoAPublicarDto>> GravarPendenteAsync(
        DocumentoAPublicar documento,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
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

    private static FinalidadeDoDocumento? FinalidadeDeclarada(string? codigo) =>
        CatalogoDe<FinalidadeDoDocumento>.TentarObter(codigo, out FinalidadeDoDocumento finalidade) ? finalidade : null;
}
