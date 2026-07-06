namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Interfaces;
using Kernel.Results;

public static class DefinirEtapasCommandHandler
{
    public static async Task<Result> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        List<EtapaProcesso> etapas = [.. command.Etapas.Select(e =>
            EtapaProcesso.Criar(e.Nome, e.Carater, e.Peso, e.NotaMinima, e.Ordem))];

        Result result = processo.DefinirEtapas(etapas);
        if (result.IsFailure)
        {
            return result;
        }

        // O agregado vem tracked de ObterComConfiguracaoAsync: a substituição
        // da coleção (Clear + novos filhos com Guid v7 já preenchido) é
        // persistida por change detection no SaveChanges. NÃO chamar
        // DbSet.Update aqui — ele marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
