namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler do <see cref="DefinirAtendimentoEspecializadoCommand"/> (CA-06 da Story #758): resolve cada dimensão nos
/// cadastros vivos do módulo Configuração (ver <see cref="CadastrosDoAtendimentoEspecializado"/>), congela por valor
/// (snapshot-copy, ADR-0061) e monta o atendimento especializado — a invariante ADR-0067 (tipo de deficiência só sob
/// condição PcD) é garantida por <see cref="AtendimentoEspecializado.Criar"/>.
/// </summary>
public static class DefinirAtendimentoEspecializadoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirAtendimentoEspecializadoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CadastrosDoAtendimentoEspecializado cadastros,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(cadastros);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, cadastros, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// O agregado está rastreado (<c>ObterParaMutacaoAsync</c>): o novo atendimento e suas filhas (Guid v7 já
    /// preenchido) são gravados pela detecção de mudanças. Não chamar <c>DbSet.Update</c>: marcaria as filhas novas
    /// como modificadas e emitiria UPDATE de linhas nunca inseridas.
    /// </remarks>
    private static async Task<Result> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirAtendimentoEspecializadoCommand command,
        CadastrosDoAtendimentoEspecializado cadastros,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result definido = await DefinirAsync(processo, command, cadastros, cancellationToken).ConfigureAwait(false);
        if (definido.IsFailure)
        {
            return definido;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <remarks>
    /// A validação precede a leitura (ADR-0125, ponto 5): um lote com identificador repetido não precisa de nenhuma
    /// consulta aos cadastros para saber que será recusado.
    /// </remarks>
    private static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo,
        DefinirAtendimentoEspecializadoCommand command,
        CadastrosDoAtendimentoEspecializado cadastros,
        CancellationToken cancellationToken)
    {
        List<FieldError> repetidos = AtendimentoEspecializado.ValidarIdsUnicos(command.CondicaoIds, command.RecursoIds, command.TipoDeficienciaIds);
        if (repetidos.Count > 0)
        {
            return Result.ValidationFailure(repetidos);
        }

        Result<AtendimentoEspecializado> montado = await cadastros.MontarAsync(command, cancellationToken).ConfigureAwait(false);
        if (montado.IsFailure)
        {
            return Result.ValidationFailure(montado.Errors);
        }

        Result definido = processo.DefinirAtendimentoEspecializado(montado.Value!);
        return definido.IsFailure ? Result.Failure(definido.Error!) : definido;
    }
}
