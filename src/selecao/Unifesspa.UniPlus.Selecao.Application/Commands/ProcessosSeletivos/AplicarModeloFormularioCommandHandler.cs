namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Copia o modelo de formulário para o formulário do código informado, que o processo já precisa ter, pelas mesmas
/// conferências da escrita direta (ver
/// <see cref="AplicacaoDoModeloDeFormulario"/>): as páginas, os itens e os termos passam pelos resolvedores
/// da escrita do formulário, e o estado final é conferido pelo agregado, que só então muda o processo
/// (ADR-0125). O que o catálogo mudou depois da composição do modelo sai da cópia e volta no relatório.
/// O estado do processo é conferido antes de qualquer leitura externa.
/// </summary>
public static class AplicarModeloFormularioCommandHandler
{
    public static async Task<Result<AplicacaoDeModeloDto>> Handle(
        AplicarModeloFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IModeloFormularioReader modeloFormularioReader,
        CadastrosDaAplicacaoDoModelo cadastros,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(modeloFormularioReader);
        ArgumentNullException.ThrowIfNull(cadastros);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return obtido.IsFailure
            ? Result<AplicacaoDeModeloDto>.Failure(obtido.Error!)
            : await ConferirEAplicarAsync(command, obtido.Value!, cadastros, modeloFormularioReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<AplicacaoDeModeloDto>> ConferirEAplicarAsync(
        AplicarModeloFormularioCommand command,
        ProcessoSeletivo processo,
        CadastrosDaAplicacaoDoModelo cadastros,
        IModeloFormularioReader modeloFormularioReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result<FormularioProcesso> formulario = FormularioDoPedido(command.FormularioCodigo, processo);
        Result<ModeloFormularioView> modelo = await ModeloAplicavelAsync(command, processo, modeloFormularioReader, cancellationToken).ConfigureAwait(false);
        return formulario.IsFailure || modelo.IsFailure
            ? Result<AplicacaoDeModeloDto>.ValidationFailure([.. formulario.Errors, .. modelo.Errors])
            : await new AplicacaoDoModeloDeFormulario(processo, formulario.Value!, modelo.Value!, cadastros, unitOfWork)
                .AplicarAsync(cancellationToken).ConfigureAwait(false);
    }

    // A cópia substitui o conteúdo do formulário
    // declarado: a fase e a obrigatoriedade não vêm do modelo, então o formulário precisa existir.
    private static Result<FormularioProcesso> FormularioDoPedido(string? formularioCodigo, ProcessoSeletivo processo)
    {
        Result<CodigoDoFormulario> codigo = CodigoDoFormulario.Criar(formularioCodigo);
        return codigo.IsFailure
            ? Result<FormularioProcesso>.ValidationFailure([new("formularioCodigo", codigo.Error!)])
            : FormularioExistente(processo, codigo.Value!);
    }

    private static Result<FormularioProcesso> FormularioExistente(ProcessoSeletivo processo, CodigoDoFormulario codigo) =>
        processo.Declaracao().Coleta.FormularioDe(codigo) is { } existente
            ? Result<FormularioProcesso>.Success(existente)
            : Result<FormularioProcesso>.ValidationFailure([new("formularioCodigo", RegrasDosFormularios.FormularioInexistente(codigo))]);

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
