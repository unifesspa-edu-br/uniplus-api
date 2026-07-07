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

        List<Guid> idsInformados = [.. command.Etapas.Where(e => e.Id.HasValue).Select(e => e.Id!.Value)];
        if (idsInformados.Distinct().Count() != idsInformados.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.IdEtapaDuplicado",
                "O mesmo Id de etapa não pode ser informado mais de uma vez no mesmo payload."));
        }

        // Reconcilia por Id em vez de recriar toda a coleção: uma etapa cujo
        // Id (ecoado pelo cliente a partir da leitura anterior) ainda existe
        // no processo é ATUALIZADA na mesma instância tracked, preservando o
        // etapa_ref que critérios de desempate/eliminação da classificação
        // possam ter — do contrário, todo PUT /etapas geraria etapas com Id
        // novo e invalidaria essas referências por construção.
        Dictionary<Guid, EtapaProcesso> existentes = processo.Etapas.ToDictionary(e => e.Id);
        List<EtapaProcesso> etapas = [];
        foreach (EtapaProcessoInput input in command.Etapas)
        {
            if (input.Id is { } id && existentes.TryGetValue(id, out EtapaProcesso? etapaExistente))
            {
                etapaExistente.AtualizarDados(input.Nome, input.Carater, input.Peso, input.NotaMinima, input.Ordem);
                etapas.Add(etapaExistente);
            }
            else
            {
                etapas.Add(EtapaProcesso.Criar(input.Nome, input.Carater, input.Peso, input.NotaMinima, input.Ordem));
            }
        }

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
