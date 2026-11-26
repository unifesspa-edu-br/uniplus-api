namespace Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Inicia o envio para o rascunho ou processo existente, sem bloquear o dono. O modelo pendente criado
/// em paralelo com a exclusão do rascunho fica sem dono e é removido quando vence, pela rotina dos pendentes vencidos.
/// </summary>
public static class IniciarEnvioDoModeloDeDocumentoCommandHandler
{
    public static async Task<Result<IniciarEnvioDoModeloDeDocumentoDto>> Handle(
        IniciarEnvioDoModeloDeDocumentoCommand command,
        IUnidadeDonaDoProcesso donos,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IArquivoArmazenadoStorage storage,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(donos);
        ArgumentNullException.ThrowIfNull(modeloDeDocumentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        return await donos.DoProcessoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false) is null
            ? Result<IniciarEnvioDoModeloDeDocumentoDto>.Failure(AcessoAoProcesso.NaoEncontrado())
            : await IniciarAsync(command, modeloDeDocumentoRepository, storage, unitOfWork, clock, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IniciarEnvioDoModeloDeDocumentoDto>> IniciarAsync(
        IniciarEnvioDoModeloDeDocumentoCommand command,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IArquivoArmazenadoStorage storage,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        Result<ModeloDeDocumento> pendente = ModeloDeDocumento.IniciarPendente(
            command.ProcessoSeletivoId, command.NomeArquivo, command.Formato, clock, PrazosDoArquivoEnviado.Envio);
        if (pendente.IsFailure)
        {
            return Result<IniciarEnvioDoModeloDeDocumentoDto>.ValidationFailure(pendente.Errors);
        }

        return await GravarPendenteAsync(pendente.Value!, modeloDeDocumentoRepository, storage, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IniciarEnvioDoModeloDeDocumentoDto>> GravarPendenteAsync(
        ModeloDeDocumento modelo, IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IArquivoArmazenadoStorage storage, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        // A URL sai antes de persistir: se o storage falhar, nada é gravado, e o retry com a mesma
        // Idempotency-Key não deixa um pendente órfão.
        string urlUpload = await storage
            .GerarUrlUploadAsync(modelo.ObjectKey, modelo.ContentType, PrazosDoArquivoEnviado.Envio, cancellationToken)
            .ConfigureAwait(false);

        await modeloDeDocumentoRepository.AdicionarAsync(modelo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<IniciarEnvioDoModeloDeDocumentoDto>.Success(new IniciarEnvioDoModeloDeDocumentoDto(
            modelo.Id, new Uri(urlUpload, UriKind.Absolute), modelo.ContentType, modelo.ExpiraEm));
    }
}
