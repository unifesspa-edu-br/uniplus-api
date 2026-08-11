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

        // Reconcilia por Id em vez de recriar toda a coleção: uma etapa cujo
        // Id (ecoado pelo cliente a partir da leitura anterior) ainda existe
        // no processo é ATUALIZADA na mesma instância tracked, preservando o
        // etapa_ref que critérios de desempate/eliminação da classificação
        // possam ter — do contrário, todo PUT /etapas geraria etapas com Id
        // novo e invalidaria essas referências por construção.
        Dictionary<Guid, EtapaProcesso> existentes = processo.Etapas.ToDictionary(e => e.Id);

        // Resolvido contra o cadastro corrente SÓ quando o vínculo é novo ou muda de tipo —
        // nunca para uma etapa existente cujo TipoEtapaOrigemId não mudou. Sem essa distinção,
        // (a) desativar um tipo já vinculado bloquearia QUALQUER PUT subsequente da coleção
        // inteira, mesmo editando só o Peso de uma etapa não relacionada; e (b) renomear um
        // tipo ainda ativo reescreveria silenciosamente o snapshot já congelado de uma etapa
        // que o cliente nem tocou — violando o próprio propósito do snapshot-copy (ADR-0061):
        // a cópia só muda quando o vínculo muda, não quando o cadastro de origem muda.
        Dictionary<Guid, TipoEtapaSnapshot> snapshotsResolvidos = [];
        List<EtapaProcesso> etapas = [];
        foreach (EtapaProcessoInput input in command.Etapas)
        {
            EtapaProcesso? etapaExistente = input.Id is { } id && existentes.TryGetValue(id, out EtapaProcesso? candidata)
                ? candidata
                : null;

            TipoEtapaSnapshot tipoEtapa;
            if (etapaExistente is not null && etapaExistente.TipoEtapaOrigemId == input.TipoEtapaOrigemId)
            {
                tipoEtapa = etapaExistente.TipoEtapa;
            }
            else if (snapshotsResolvidos.TryGetValue(input.TipoEtapaOrigemId, out TipoEtapaSnapshot? snapshotEmCache))
            {
                tipoEtapa = snapshotEmCache;
            }
            else
            {
                TipoEtapaView? tipo = await tipoEtapaReader
                    .ObterAtivoPorIdAsync(input.TipoEtapaOrigemId, cancellationToken)
                    .ConfigureAwait(false);
                if (tipo is null)
                {
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo",
                        $"Tipo de etapa {input.TipoEtapaOrigemId} não encontrado ou não está ativo."));
                }

                Result<TipoEtapaSnapshot> snapshotResult = TipoEtapaSnapshot.Criar(tipo.Id, tipo.Codigo, tipo.Nome);
                if (snapshotResult.IsFailure)
                {
                    return Result<MutacaoAceita>.Failure(snapshotResult.Error!);
                }

                tipoEtapa = snapshotResult.Value!;
                snapshotsResolvidos[input.TipoEtapaOrigemId] = tipoEtapa;
            }

            if (etapaExistente is not null)
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
