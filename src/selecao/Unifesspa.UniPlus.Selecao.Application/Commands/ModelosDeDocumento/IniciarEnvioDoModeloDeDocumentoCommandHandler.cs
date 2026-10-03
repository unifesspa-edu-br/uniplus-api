namespace Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Services;

/// <summary>
/// Handler de <see cref="IniciarEnvioDoModeloDeDocumentoCommand"/>: confere que o processo existe,
/// cria o registro pendente e devolve a URL de PUT. Enviar o modelo não é configuração do processo
/// — o que a muda é vincular o modelo a uma exigência —, então não passa pela trava de mutação.
/// </summary>
public static class IniciarEnvioDoModeloDeDocumentoCommandHandler
{
    public static async Task<Result<IniciarEnvioDoModeloDeDocumentoDto>> Handle(
        IniciarEnvioDoModeloDeDocumentoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IArquivoArmazenadoStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(modeloDeDocumentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        if (!await processoSeletivoRepository.ExisteAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false))
        {
            return Result<IniciarEnvioDoModeloDeDocumentoDto>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado", "Processo Seletivo não encontrado."));
        }

        Result<ModeloDeDocumento> pendente = ModeloDeDocumento.IniciarPendente(
            command.ProcessoSeletivoId, command.NomeArquivo, command.Formato, clock, PrazosDoArquivoEnviado.Envio);
        if (pendente.IsFailure)
        {
            return Result<IniciarEnvioDoModeloDeDocumentoDto>.ValidationFailure(pendente.Errors);
        }

        ModeloDeDocumento modelo = pendente.Value!;

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
