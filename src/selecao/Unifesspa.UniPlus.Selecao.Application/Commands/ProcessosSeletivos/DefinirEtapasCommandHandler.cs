namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

public static class DefinirEtapasCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ISelecaoUnitOfWork unitOfWork,
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
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição é conferida AQUI, logo depois do 404 e antes das regras de negócio
        // que este handler avalia (existência de cadastros, coerência de referências): ela
        // as precede na ordem da ADR-0110 D9. Um cliente com If-Match defasado tem de saber
        // disso antes de sair caçando um cadastro que ele não errou.
        //
        // O que ela NÃO precede é a validação de SCHEMA do payload: o FluentValidation roda
        // como middleware do Wolverine, antes deste handler, e um command malformado morre
        // ali com 422 sem que o guard chegue a rodar. É desvio consciente da D9 — corrigi-lo
        // exigiria carregar o agregado no middleware, o que é pior. O custo é uma rodada
        // extra para quem erra as DUAS coisas ao mesmo tempo; nenhum estado é corrompido.
        //
        // O mesmo guard continua dentro do Definir* do domínio: esta antecipação dá a ordem,
        // não a garantia.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        List<Guid> idsInformados = [.. command.Etapas.Where(e => e.Id.HasValue).Select(e => e.Id!.Value)];
        if (idsInformados.Distinct().Count() != idsInformados.Count)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
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

        Result result = processo.DefinirEtapas(etapas, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        // O agregado vem tracked de ObterParaMutacaoAsync: a substituição
        // da coleção (Clear + novos filhos com Guid v7 já preenchido) é
        // persistida por change detection no SaveChanges. NÃO chamar
        // DbSet.Update aqui — ele marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
