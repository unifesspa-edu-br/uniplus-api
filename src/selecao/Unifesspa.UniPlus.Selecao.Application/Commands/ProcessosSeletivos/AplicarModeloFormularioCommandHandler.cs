namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Copia o modelo de formulário para o processo pelas mesmas conferências da escrita direta (ver
/// <see cref="AplicacaoDoModeloDeFormulario"/>): as etapas, os itens e os termos passam pelos resolvedores
/// da escrita do formulário, e o estado final é conferido pelo agregado, que só então muda o processo
/// (ADR-0125). O que o catálogo mudou depois da composição do modelo sai da cópia e volta no relatório.
/// </summary>
public static class AplicarModeloFormularioCommandHandler
{
    public static async Task<Result<AplicacaoDeModeloDto>> Handle(
        AplicarModeloFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IModeloFormularioReader modeloFormularioReader,
        IFatoCandidatoReader fatoCandidatoReader,
        ITermoConsentimentoReader termoConsentimentoReader,
        ITipoDocumentoReader tipoDocumentoReader,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(modeloFormularioReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<AplicacaoDeModeloDto>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        // O estado do processo é conferido antes de qualquer leitura externa.
        if (processo.RecusaDeAplicarModelo(command.Precondicao) is { } recusaDeEstado)
        {
            return Result<AplicacaoDeModeloDto>.Failure(recusaDeEstado);
        }

        Result<ModeloFormularioView> modelo = await ModeloAplicavelAsync(command, processo, modeloFormularioReader, cancellationToken).ConfigureAwait(false);
        if (modelo.IsFailure)
        {
            return Result<AplicacaoDeModeloDto>.ValidationFailure(modelo.Errors);
        }

        CadastrosDaAplicacaoDoModelo cadastros = new(
            fatoCandidatoReader,
            termoConsentimentoReader,
            new CadastrosDosDocumentosExigidos(tipoDocumentoReader, fatoCandidatoReader, modeloDeDocumentoRepository));
        return await new AplicacaoDoModeloDeFormulario(processo, modelo.Value!, command.Precondicao, cadastros, unitOfWork)
            .AplicarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ModeloFormularioView>> ModeloAplicavelAsync(
        AplicarModeloFormularioCommand command,
        ProcessoSeletivo processo,
        IModeloFormularioReader modeloFormularioReader,
        CancellationToken cancellationToken)
    {
        ModeloFormularioView? modelo = await modeloFormularioReader.ObterAsync(command.ModeloId, cancellationToken).ConfigureAwait(false);
        if (modelo is null)
        {
            return Result<ModeloFormularioView>.ValidationFailure([new("modeloId", new DomainError(
                AplicacaoDeModeloErrorCodes.ModeloInexistente, "O modelo de formulário informado não existe."))]);
        }

        List<FieldError> erros = [.. ConferenciaDoModeloNoProcesso.Conferir(modelo, processo)];
        return erros.Count > 0
            ? Result<ModeloFormularioView>.ValidationFailure(erros)
            : Result<ModeloFormularioView>.Success(modelo);
    }
}
