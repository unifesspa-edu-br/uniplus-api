namespace Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Mappings;

using Services;

/// <summary>Handler de <see cref="ConfirmarEnvioDoModeloDeDocumentoCommand"/>.</summary>
public static class ConfirmarEnvioDoModeloDeDocumentoCommandHandler
{
    private static readonly ConfirmacaoDoArquivoEnviado.Recusas Recusas = new(
        ObjetoNaoEncontrado: new DomainError(
            ModeloDeDocumentoErrorCodes.ObjetoNaoEncontrado,
            "O arquivo ainda não foi enviado ao storage ou expirou antes da confirmação."),
        TamanhoExcedido: ModeloDeDocumento.TamanhoExcedido,
        StatusInvalido: new DomainError(
            ModeloDeDocumentoErrorCodes.StatusInvalidoParaConfirmacao, "Somente um modelo pendente pode ser confirmado."));

    public static async Task<Result<ModeloDeDocumentoDto>> Handle(
        ConfirmarEnvioDoModeloDeDocumentoCommand command,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IArquivoArmazenadoStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(modeloDeDocumentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        ModeloDeDocumento? modelo = await modeloDeDocumentoRepository
            .ObterPorIdAsync(command.ModeloDeDocumentoId, cancellationToken)
            .ConfigureAwait(false);
        if (modelo is null || modelo.ProcessoSeletivoId != command.ProcessoSeletivoId)
        {
            return Result<ModeloDeDocumentoDto>.Failure(new DomainError(
                ModeloDeDocumentoErrorCodes.NaoEncontrado, "Modelo de documento não encontrado neste Processo Seletivo."));
        }

        Result confirmacao = await ConfirmacaoDoArquivoEnviado
            .ConfirmarAsync(modelo, ModeloDeDocumento.TamanhoMaximoBytes, Recusas, modeloDeDocumentoRepository, storage, clock, cancellationToken)
            .ConfigureAwait(false);
        if (confirmacao.IsFailure)
        {
            return Result<ModeloDeDocumentoDto>.Failure(confirmacao.Error!);
        }

        modeloDeDocumentoRepository.Atualizar(modelo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ModeloDeDocumentoDto>.Success(ModeloDeDocumentoMapping.ToDto(modelo));
    }
}
