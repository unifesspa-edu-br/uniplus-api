namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

public static class DefinirEtapasCommandHandler
{
    /// <summary>
    /// SEM <c>[NonTransactional]</c>, de propósito (issue #1071): este handler depende da
    /// transação ambiente do Wolverine para <see cref="IProcessoSeletivoRepository.ObterParaMutacaoAsync"/>
    /// — o <c>SELECT ... FOR UPDATE</c> só serializa handlers concorrentes porque roda
    /// enrolado nela (ver comentário em <c>ProcessoSeletivoRepository.ObterParaMutacaoAsync</c>);
    /// <c>[NonTransactional]</c> desabilitaria esse enrolamento e o lock pessimista deixaria
    /// de bloquear qualquer coisa. <see cref="PublicarProcessoSeletivoCommandHandler"/> já prova
    /// que injetar reader cross-módulo (<see cref="ITipoEtapaReader"/>, aqui) junto do mesmo
    /// lock não é ambíguo para o <c>AutoApplyTransactions</c> — só um handler que injeta
    /// diretamente um <em>segundo DbContext concreto</em> (não um reader por trás de interface
    /// pública) precisaria do opt-in.
    /// </summary>
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoEtapaReader tipoEtapaReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
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
        // disso antes de sair caçando um cadastro que ele não errou — inclusive antes da
        // resolução de TipoEtapaOrigemId contra o cadastro de Configuração, que só roda a
        // seguir.
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

        Dictionary<Guid, TipoEtapaSnapshot> snapshotsPorTipoOrigemId = [];
        foreach (Guid tipoEtapaOrigemId in command.Etapas.Select(e => e.TipoEtapaOrigemId).Distinct())
        {
            TipoEtapaView? tipo = await tipoEtapaReader
                .ObterAtivoPorIdAsync(tipoEtapaOrigemId, cancellationToken)
                .ConfigureAwait(false);
            if (tipo is null)
            {
                return Result<MutacaoAceita>.Failure(new DomainError(
                    "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo",
                    $"Tipo de etapa {tipoEtapaOrigemId} não encontrado ou não está ativo."));
            }

            Result<TipoEtapaSnapshot> snapshotResult = TipoEtapaSnapshot.Criar(tipo.Id, tipo.Codigo, tipo.Nome);
            if (snapshotResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(snapshotResult.Error!);
            }

            snapshotsPorTipoOrigemId[tipoEtapaOrigemId] = snapshotResult.Value!;
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
            TipoEtapaSnapshot tipoEtapa = snapshotsPorTipoOrigemId[input.TipoEtapaOrigemId];

            if (input.Id is { } id && existentes.TryGetValue(id, out EtapaProcesso? etapaExistente))
            {
                etapaExistente.AtualizarDados(
                    input.Nome, input.Carater, tipoEtapa, input.Peso, input.NotaMinima, input.Ordem);
                etapas.Add(etapaExistente);
            }
            else
            {
                etapas.Add(EtapaProcesso.Criar(
                    input.Nome, input.Carater, tipoEtapa, input.Peso, input.NotaMinima, input.Ordem));
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
