namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler do <see cref="DefinirIdentificadorLegivelCommand"/>. A regra de quando o identificador
/// pode mudar é da raiz (<see cref="ProcessoSeletivo.DefinirIdentificadorLegivel"/>); aqui ficam
/// o que depende de consulta — a unicidade entre processos — e a validação do formato.
/// </summary>
public static class DefinirIdentificadorLegivelCommandHandler
{
    public static async Task<Result> Handle(
        DefinirIdentificadorLegivelCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(command, obtido.Value!, processoSeletivoRepository, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// A recusa do formato vem antes da recusa da raiz, e a da raiz antes da unicidade: quem não pode alterar
    /// recebe essa causa, e não um conflito com outro processo que nada muda para ele.
    /// </summary>
    private static async Task<Result> DefinirEGravarAsync(
        DefinirIdentificadorLegivelCommand command,
        ProcessoSeletivo processo,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result<IdentificadorLegivel> declarado = IdentificadorLegivelDeclarado.Conferir(command.IdentificadorLegivel);
        if (declarado.IsFailure)
        {
            return Result.ValidationFailure(declarado.Errors);
        }

        Result definido = processo.DefinirIdentificadorLegivel(declarado.Value!);
        return definido.IsFailure
            ? Result.Failure(definido.Error!)
            : await GravarSeLivreAsync(processo, declarado.Value!, processoSeletivoRepository, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// A raiz já está alterada quando a unicidade é consultada. Toda recusa daqui em diante descarta o
    /// rastreamento: sem isso, o <c>SaveChangesAsync</c> que o Wolverine dispara depois do handler gravaria a
    /// alteração recusada e cairia no índice único como erro interno.
    /// </summary>
    private static async Task<Result> GravarSeLivreAsync(
        ProcessoSeletivo processo,
        IdentificadorLegivel identificador,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        bool emUso = await processoSeletivoRepository
            .IdentificadorLegivelEmUsoAsync(identificador, processo.Id, cancellationToken)
            .ConfigureAwait(false);

        return emUso
            ? RecusaDoIdentificadorEmUso(unitOfWork)
            : await GravarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Outro processo pode gravar o mesmo identificador entre a consulta de uso e esta gravação; o índice único
    /// é quem decide, e o conflito sai como a mesma recusa da consulta.
    /// </summary>
    private static async Task<Result> GravarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CriarProcessoSeletivoCommandHandler.EhConflitoDeIdentificadorLegivel(ex))
        {
            return RecusaDoIdentificadorEmUso(unitOfWork);
        }

        return Result.Success();
    }

    private static Result RecusaDoIdentificadorEmUso(IUnitOfWork unitOfWork)
    {
        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result.Failure(IdentificadorLegivelDeclarado.EmUso());
    }
}
