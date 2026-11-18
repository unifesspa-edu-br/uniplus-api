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

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // Fora do rascunho a recusa sai antes da validação do formato.
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        Result<IdentificadorLegivel> identificadorResult =
            CriarProcessoSeletivoCommandHandler.IdentificadorLegivelDeclarado(command.IdentificadorLegivel);
        if (identificadorResult.IsFailure)
        {
            return Result.Failure(identificadorResult.Error!);
        }

        IdentificadorLegivel identificador = identificadorResult.Value!;

        // A recusa da raiz vem primeiro: quem não pode alterar recebe essa causa, e não um
        // conflito com outro processo que nada muda para ele.
        Result definirResult = processo.DefinirIdentificadorLegivel(identificador);
        if (definirResult.IsFailure)
        {
            return Result.Failure(definirResult.Error!);
        }

        // Daqui em diante a raiz já está alterada. Toda recusa descarta o rastreamento: sem isso,
        // o SaveChangesAsync que o Wolverine dispara depois do handler gravaria a alteração
        // recusada e cairia no índice único como erro interno.
        if (await processoSeletivoRepository
                .IdentificadorLegivelEmUsoAsync(identificador, processo.Id, cancellationToken)
                .ConfigureAwait(false))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(CriarProcessoSeletivoCommandHandler.IdentificadorLegivelEmUso(identificador));
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CriarProcessoSeletivoCommandHandler.EhConflitoDeIdentificadorLegivel(ex))
        {
            // Outro processo gravou o mesmo identificador entre a consulta acima e esta gravação.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(CriarProcessoSeletivoCommandHandler.IdentificadorLegivelEmUso(identificador));
        }

        return Result.Success();
    }
}
