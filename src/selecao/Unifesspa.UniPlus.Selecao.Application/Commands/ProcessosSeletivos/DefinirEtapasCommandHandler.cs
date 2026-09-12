namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
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
    /// <remarks>
    /// Em duas passadas (ADR-0125): a primeira acumula a forma de TODAS as etapas via
    /// <see cref="EtapaProcesso.ValidarFormaBasica"/>, sem tocar o <see cref="ITipoEtapaReader"/>;
    /// só se ela vier limpa a segunda resolve cada tipo de etapa no cadastro e chama
    /// <see cref="EtapaProcesso.Criar"/>/<see cref="EtapaProcesso.AtualizarDados"/>, que
    /// reinvocam a mesma checagem como defesa em profundidade.
    /// </remarks>
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoEtapaReader tipoEtapaReader,
        ITipoBancaReader tipoBancaReader,
        IRegraCatalogoReader regraCatalogoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
        ArgumentNullException.ThrowIfNull(tipoBancaReader);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
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

        // Acumula (ADR-0125) a forma de TODAS as etapas do payload numa primeira passada, com
        // EtapaProcesso.ValidarFormaBasica — nenhuma dessas checagens depende do cadastro de
        // tipos de etapa. Só roda antes do tipoEtapaReader (validação vence I/O dentro do
        // bucket 422, PR #1211): sem isso, um payload com 2 etapas — uma com nome vazio, outra
        // com TipoEtapaOrigemId de um tipo inativo — devolveria só o erro de catálogo da
        // segunda etapa, escondendo a falha de forma da primeira.
        List<FieldError> formaErros = [];
        for (int indice = 0; indice < command.Etapas.Count; indice++)
        {
            EtapaProcessoInput itemForma = command.Etapas[indice];
            formaErros.AddRange(EtapaProcesso
                .ValidarFormaBasica(itemForma.Nome, itemForma.Carater, itemForma.Peso, itemForma.NotaMinima, itemForma.Ordem)
                .Select(erro => erro.Field is null ? erro : erro with { Field = $"etapas[{indice}].{erro.Field}" }));
        }

        if (formaErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(formaErros);
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
        //
        // O cache guarda o TipoEtapaView (dado bruto do catálogo, evita reconsultar o reader
        // quando duas etapas NOVAS ou realinhadas compartilham o mesmo tipo), NUNCA a instância
        // de TipoEtapaSnapshot já construída — TipoEtapa é owned type (OwnsOne) de EtapaProcesso,
        // e EF Core não aceita a MESMA instância pertencendo a dois donos ao mesmo tempo; duas
        // etapas com o mesmo tipo, cada uma com sua própria instância, é caso legítimo e comum.
        Dictionary<Guid, TipoEtapaView> tiposEmCache = [];
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
            else
            {
                if (!tiposEmCache.TryGetValue(input.TipoEtapaOrigemId, out TipoEtapaView? tipo))
                {
                    tipo = await tipoEtapaReader
                        .ObterAtivoPorIdAsync(input.TipoEtapaOrigemId, cancellationToken)
                        .ConfigureAwait(false);
                    if (tipo is null)
                    {
                        // Uma etapa ANTERIOR no mesmo payload pode já ter mutado uma instância
                        // tracked (AtualizarDados) antes desta falhar — sem descartar, o
                        // SaveChangesAsync automático do Wolverine (AutoApplyTransactions)
                        // persistiria essa mutação parcial mesmo com o PUT inteiro recusado.
                        unitOfWork.DescartarAlteracoesNaoSalvas();
                        return Result<MutacaoAceita>.Failure(new DomainError(
                            "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo",
                            $"Tipo de etapa {input.TipoEtapaOrigemId} não encontrado ou não está ativo."));
                    }

                    tiposEmCache[input.TipoEtapaOrigemId] = tipo;
                }

                Result<TipoEtapaSnapshot> snapshotResult = TipoEtapaSnapshot.Criar(tipo.Id, tipo.Codigo, tipo.Nome);
                if (snapshotResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(snapshotResult.Error!);
                }

                tipoEtapa = snapshotResult.Value!;
            }

            if (etapaExistente is not null)
            {
                // Defesa em profundidade: ValidarFormaBasica já rodou limpa na primeira
                // passada acima — esta falha só ocorreria por dessincronia entre as duas
                // checagens, nunca pelo payload em si.
                Result atualizarResult = etapaExistente.AtualizarDados(
                    input.Nome, input.Carater, tipoEtapa, input.Peso, input.NotaMinima, input.Ordem,
                    input.FaseCodigo);
                if (atualizarResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.ValidationFailure(atualizarResult.Errors);
                }

                etapas.Add(etapaExistente);
            }
            else
            {
                Result<EtapaProcesso> criarResult = EtapaProcesso.Criar(
                    input.Nome, input.Carater, tipoEtapa, input.Peso, input.NotaMinima, input.Ordem,
                    input.FaseCodigo);
                if (criarResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.ValidationFailure(criarResult.Errors);
                }

                etapas.Add(criarResult.Value!);
            }
        }

        // Os produtos são declarados por etapa, depois que a etapa existe: é ela quem os
        // vincula, e a recusa de ato repetido é dela.
        for (int i = 0; i < etapas.Count; i++)
        {
            IReadOnlyList<ProdutoDaEtapaInput> declarados = command.Etapas[i].Produtos ?? [];
            Result produtosResult = etapas[i].DefinirProdutos(
                [.. declarados.Select(d =>
                {
                    PapelProdutoFaseCodigo.TentarConverter(d.Papel, out PapelProdutoFase? papel);
                    return ProdutoDaEtapa.Criar(d.AtoCodigo, papel);
                })]);
            if (produtosResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(produtosResult.Error!);
            }

            EtapaProcessoInput input = command.Etapas[i];

            Result janelaResult = etapas[i].DefinirJanelaEParecer(
                input.Inicio, input.Fim, input.EmiteParecerIndividual);
            if (janelaResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(janelaResult.Error!);
            }

            List<BancaDaEtapa> bancas = [];
            foreach (BancaDaEtapaInput bancaInput in input.Bancas ?? [])
            {
                TipoBancaView? tipoBanca = await tipoBancaReader
                    .ObterPorIdAsync(bancaInput.TipoBancaId, cancellationToken)
                    .ConfigureAwait(false);
                if (tipoBanca is null)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "EtapaProcesso.TipoBancaNaoEncontradoOuInativo",
                        $"O tipo de banca {bancaInput.TipoBancaId} não existe ou está inativo no cadastro."));
                }

                bancas.Add(BancaDaEtapa.Criar(tipoBanca.Id, tipoBanca.Codigo));
            }

            Result bancasResult = etapas[i].DefinirBancas(bancas);
            if (bancasResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(bancasResult.Error!);
            }

            // A âncora em ato é declarada pelo código, e resolvida DENTRO dos produtos desta
            // etapa: ancorar na publicação de outra etapa deixa de ser exprimível.
            List<RecursoDaEtapa> recursos = [];
            foreach (RecursoDaEtapaInput declarado in input.Recursos ?? [])
            {
                Guid ancora = declarado.Ancora == AncoraDoRecurso.AtoPublicado
                    ? etapas[i].Produtos
                        .FirstOrDefault(pr => string.Equals(pr.AtoCodigo, declarado.AtoAncoraCodigo, StringComparison.Ordinal))
                        ?.Id ?? Guid.Empty
                    : Guid.Empty;

                ArgsRegraPrazoRecurso args = new(
                    declarado.PrazoValor,
                    declarado.PrazoUnidade,
                    declarado.SuspensividadePrimeiraInstanciaValor,
                    declarado.SuspensividadePrimeiraInstanciaUnidade,
                    declarado.SuspensividadeSegundaInstanciaValor,
                    declarado.SuspensividadeSegundaInstanciaUnidade);

                // A referência é montada a partir dos valores RESOLVIDOS do catálogo, nunca
                // ecoados do payload: é o que faz o hash bater por construção. E a recusa é
                // de validação, nunca exceção — catálogo que não respondeu na tela chega
                // aqui com código em branco, e o cliente precisa da mensagem, não de um 500.
                RegraCatalogo? regraCatalogo = await regraCatalogoReader
                    .ObterAsync(declarado.RegraCodigo, declarado.RegraVersao, cancellationToken)
                    .ConfigureAwait(false);
                if (regraCatalogo is null || regraCatalogo.Tipo != TipoRegra.RegraPrazoRecurso)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "RecursoDaEtapa.RegraCatalogoInvalida",
                        $"A regra {declarado.RegraCodigo}/{declarado.RegraVersao} não é uma regra de prazo de recurso do catálogo."));
                }

                Result<ReferenciaRegra> regraResult =
                    ReferenciaRegra.Criar(regraCatalogo.Codigo, regraCatalogo.Versao, regraCatalogo.Hash);
                if (regraResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.ValidationFailure(regraResult.Errors);
                }

                Result<RecursoDaEtapa> recursoResult = RecursoDaEtapa.Criar(
                    declarado.Ancora,
                    regraResult.Value!,
                    args,
                    ancora);
                if (recursoResult.IsFailure)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.ValidationFailure(recursoResult.Errors);
                }

                recursos.Add(recursoResult.Value!);
            }

            Result recursosResult = etapas[i].DefinirRecursos(recursos);
            if (recursosResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(recursosResult.Error!);
            }
        }

        Result result = processo.DefinirEtapas(etapas, command.Precondicao);
        if (result.IsFailure)
        {
            // Mesmo motivo do descarte acima: etapas existentes já foram mutadas
            // (AtualizarDados) no loop de reconciliação antes desta checagem — sem
            // descartar, o SaveChangesAsync automático do Wolverine persistiria a
            // mutação parcial mesmo com DefinirEtapas tendo recusado a coleção inteira.
            unitOfWork.DescartarAlteracoesNaoSalvas();
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
