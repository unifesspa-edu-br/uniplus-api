namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

public static class DefinirEtapasCommandHandler
{
    /// <summary>
    /// SEM <c>[NonTransactional]</c>, de propósito (issue #1071): este handler depende da
    /// transação ambiente do Wolverine para <see cref="IProcessoSeletivoRepository.ObterParaMutacaoAsync"/>
    /// — o <c>SELECT ... FOR UPDATE</c> só serializa handlers concorrentes porque roda
    /// enrolado nela (ver comentário em <c>ProcessoSeletivoRepository.ObterParaMutacaoAsync</c>);
    /// <c>[NonTransactional]</c> desabilitaria esse enrolamento e o lock pessimista deixaria
    /// de bloquear qualquer coisa. <see cref="PublicarProcessoSeletivoCommandHandler"/> já prova
    /// que injetar reader cross-módulo (aqui, <see cref="ITipoEtapaReader"/> sobre o cadastro de
    /// Configuração e <see cref="ITipoAtoPublicadoReader"/> sobre o catálogo de Publicações) junto
    /// do mesmo lock não é ambíguo para o <c>AutoApplyTransactions</c> — só um handler que injeta
    /// diretamente um <em>segundo DbContext concreto</em> (não um reader por trás de interface
    /// pública) precisaria do opt-in. O que mantém o segundo DbContext fora da árvore que o
    /// codegen enxerga é o <c>AlwaysUseServiceLocationFor</c> declarado para cada um desses
    /// contratos: um reader novo sem esse opt-in reintroduz a ambiguidade de DbContext e derruba
    /// o enrolamento transacional de que o lock depende.
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
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        TimeProvider timeProvider,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
        ArgumentNullException.ThrowIfNull(tipoBancaReader);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(tipoAtoPublicadoReader);
        ArgumentNullException.ThrowIfNull(timeProvider);
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

        // O caráter declarado é conferido contra o que o tipo admite no cadastro — e numa passada
        // própria, ANTES de qualquer mutação: assim um payload com duas etapas incoerentes
        // devolve as duas (ADR-0125), e nenhuma instância tracked precisa ser descartada.
        //
        // A passada só lê o cadastro quando há o que conferir: etapa existente que mantém o
        // vínculo E o caráter não é reavaliada. Sem esse recorte, estreitar um tipo ainda ATIVO
        // — marcar que ele deixou de compor a nota final, que é justamente a operação que este
        // cadastro existe para permitir — passaria a recusar todo PUT posterior de qualquer
        // certame que já tivesse uma etapa daquele tipo, mesmo editando só o nome de outra.
        List<FieldError> caraterErros = [];
        for (int indice = 0; indice < command.Etapas.Count; indice++)
        {
            EtapaProcessoInput itemCarater = command.Etapas[indice];
            EtapaProcesso? jaExistente = itemCarater.Id is { } idExistente
                && existentes.TryGetValue(idExistente, out EtapaProcesso? emCurso) ? emCurso : null;
            bool vinculoInalterado = jaExistente is not null
                && jaExistente.TipoEtapaOrigemId == itemCarater.TipoEtapaOrigemId;
            if (vinculoInalterado && jaExistente!.Carater == itemCarater.Carater)
            {
                continue;
            }

            if (!tiposEmCache.TryGetValue(itemCarater.TipoEtapaOrigemId, out TipoEtapaView? tipoParaConferir))
            {
                tipoParaConferir = await tipoEtapaReader
                    .ObterAtivoPorIdAsync(itemCarater.TipoEtapaOrigemId, cancellationToken)
                    .ConfigureAwait(false);
                if (tipoParaConferir is null)
                {
                    // Vínculo inalterado e tipo desde então desativado: não há de onde ler o que
                    // ele admite, e recusar puniria quem só quer corrigir o caráter de uma etapa
                    // que já existia. Segue sem conferir — é o mesmo tratamento que o snapshot
                    // já congelado recebe logo abaixo.
                    if (vinculoInalterado)
                    {
                        continue;
                    }

                    // Acumula com as recusas de caráter em vez de sair na primeira: quem
                    // errou o tipo de uma etapa e o caráter de outra corrige as duas de uma
                    // vez, e não descobre a segunda só na tentativa seguinte.
                    caraterErros.Add(new($"etapas[{indice}].tipoEtapaOrigemId", new DomainError(
                        "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo",
                        $"Tipo de etapa {itemCarater.TipoEtapaOrigemId} não encontrado ou não está ativo.")));
                    continue;
                }

                tiposEmCache[itemCarater.TipoEtapaOrigemId] = tipoParaConferir;
            }

            caraterErros.AddRange(EtapaProcesso
                .ValidarCaraterAdmitido(
                    itemCarater.Carater,
                    tipoParaConferir.AdmitePontuacao,
                    tipoParaConferir.AdmiteEliminacao,
                    tipoParaConferir.Nome)
                .Select(erro => erro with { Field = $"etapas[{indice}].{erro.Field}" }));
        }

        if (caraterErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(caraterErros);
        }

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
                // Vínculo novo ou alterado já foi resolvido e cacheado pela passada que confere
                // o caráter, e é lá que o tipo inativo é recusado — antes de qualquer mutação.
                TipoEtapaView tipo = tiposEmCache[input.TipoEtapaOrigemId];

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

        // UMA leitura do relógio para toda a operação (ADR-0068): todo produto declarado, de
        // qualquer etapa da coleção, resolve a vigência contra o MESMO instante. Duas leituras
        // deixariam a coleção atravessar a virada do dia e aceitar metade dela.
        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Memoiza a resolução por código dentro da execução do comando, como o cronograma de
        // fases faz um nível acima. A etapa publica o mesmo ato duas vezes — preliminar e
        // definitivo —, e o número de produtos cresce com a coleção inteira: sem isto, cada item
        // de cada etapa releria o catálogo, e as idas ao banco acontecem com o SELECT ... FOR
        // UPDATE do certame na mão, serializando quem edita o mesmo certame por todo esse tempo.
        Dictionary<string, TipoAtoPublicadoView?> tiposDeAtoResolvidos = new(StringComparer.Ordinal);

        async Task<TipoAtoPublicadoView?> ResolverTipoDeAtoAsync(string codigo)
        {
            if (tiposDeAtoResolvidos.TryGetValue(codigo, out TipoAtoPublicadoView? memoizado))
            {
                return memoizado;
            }

            TipoAtoPublicadoView? resolvido = await tipoAtoPublicadoReader
                .ObterVigenteAsync(codigo, hoje, cancellationToken)
                .ConfigureAwait(false);
            tiposDeAtoResolvidos[codigo] = resolvido;
            return resolvido;
        }

        // Os produtos são declarados por etapa, depois que a etapa existe: é ela quem os
        // vincula, e a recusa de ato repetido é dela.
        for (int i = 0; i < etapas.Count; i++)
        {
            EtapaProcessoInput input = command.Etapas[i];
            IReadOnlyList<ProdutoDaEtapaInput> declarados = input.Produtos ?? [];

            // O papel desconhecido é RECUSADO, e não tratado como ausente. Ignorar a conversão
            // fazia um erro de digitação — `PRELIMINARR` por `PRELIMINAR` — virar produto sem
            // papel: a etapa deixava de publicar resultado, o recurso perdia onde ancorar, e
            // nada dizia isso a quem declarou. Mesma recusa que a fase dá um nível acima.
            List<ProdutoDaEtapa> produtosDaEtapa = [];
            foreach (ProdutoDaEtapaInput d in declarados)
            {
                if (!PapelProdutoFaseCodigo.TentarConverter(d.Papel, out PapelProdutoFase? papel))
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "ProdutoDaEtapa.PapelDesconhecido",
                        $"O papel '{d.Papel}', declarado pela etapa '{input.Nome}', não é declarável — use '{PapelProdutoFaseCodigo.Preliminar}', '{PapelProdutoFaseCodigo.Definitivo}' ou nenhum."));
                }

                // Conferido contra o catálogo SÓ quando a etapa passa a declarar este par
                // (ato, papel) — nunca para um que ela já declarava. É a mesma distinção que o
                // tipo da etapa recebe acima, e pela mesma razão: aposentar no catálogo um ato
                // já declarado bloquearia QUALQUER PUT posterior da coleção inteira, porque o
                // endpoint substitui tudo e o cliente devolve o produto que leu. Quem só quis
                // corrigir o nome de outra etapa ficaria preso, sem nada na tela dizendo que o
                // caminho de saída é apagar um produto que ele não declarou hoje.
                bool jaDeclarado = etapas[i].Produtos.Any(p =>
                    string.Equals(p.AtoCodigo, d.AtoCodigo, StringComparison.Ordinal) && p.Papel == papel);
                if (jaDeclarado)
                {
                    produtosDaEtapa.Add(ProdutoDaEtapa.Criar(d.AtoCodigo, papel));
                    continue;
                }

                // O ato precisa existir no catálogo de Publicações, pela mesma razão que a fase
                // o exige um nível acima: o código declarado aqui vira o nome do produto no
                // cronograma público e a chave pela qual a restauração da configuração congelada
                // reencontra o produto. Um código que o catálogo não conhece não resolve para
                // nada — e só apareceria depois de publicado.
                TipoAtoPublicadoView? tipoAto = await ResolverTipoDeAtoAsync(d.AtoCodigo).ConfigureAwait(false);
                if (tipoAto is null)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo",
                        $"O tipo de ato '{d.AtoCodigo}', declarado pela etapa '{input.Nome}', não tem versão vigente no catálogo de Publicações na data de hoje."));
                }

                // Papel preliminar ou definitivo só faz sentido sobre resultado: é o par que a
                // janela recursal ancora. A fase recusa o mesmo, e deixar passar aqui produziria
                // uma etapa cujo recurso aponta para um produto que nunca divulga resultado.
                if (papel is not null && !tipoAto.EhResultado)
                {
                    unitOfWork.DescartarAlteracoesNaoSalvas();
                    return Result<MutacaoAceita>.Failure(new DomainError(
                        "ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado",
                        $"O tipo de ato '{d.AtoCodigo}', declarado pela etapa '{input.Nome}', não é resultado no catálogo — só resultado recebe papel preliminar ou definitivo."));
                }

                produtosDaEtapa.Add(ProdutoDaEtapa.Criar(tipoAto.Codigo, papel));
            }

            Result produtosResult = etapas[i].DefinirProdutos(produtosDaEtapa);
            if (produtosResult.IsFailure)
            {
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result<MutacaoAceita>.Failure(produtosResult.Error!);
            }

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
                // Entre os PRELIMINARES, e não entre todos os produtos: a etapa publica o mesmo
                // ato duas vezes — preliminar e definitivo —, e é do preliminar que a janela
                // corre. Resolver só pelo código devolveria o definitivo sempre que ele viesse
                // primeiro na coleção, e a etapa seria recusada por ancorar onde não se pode.
                ProdutoDaEtapa? produtoAncora = declarado.Ancora == AncoraDoRecurso.AtoPublicado
                    ? etapas[i].Produtos
                        .FirstOrDefault(pr => pr.Papel == PapelProdutoFase.Preliminar
                            && string.Equals(pr.AtoCodigo, declarado.AtoAncoraCodigo, StringComparison.Ordinal))
                    : null;
                Guid ancora = produtoAncora?.Id ?? Guid.Empty;

                // As mesmas duas recusas que a fase dá sobre a âncora dela, e pelas mesmas
                // razões. O ato que CONGELA a configuração é o que fixa as regras do certame:
                // contar prazo de recurso a partir dele é contar contra o documento que o
                // candidato não está contestando. E o ato de EFEITO IRREVERSÍVEL concede
                // direito que não se desfaz — uma vaga escassa já entregue a outro —, então o
                // recurso não teria o que reverter; o cabível é contra o resultado que o
                // fundamentou (UNI-REQ-0080).
                //
                // Conferido sobre o produto âncora JÁ RESOLVIDO, e não sobre o código cru do
                // payload: um código que não corresponde a produto preliminar algum desta etapa
                // não é uma âncora indevida, é uma âncora que não existe, e quem sabe dizer isso
                // — nomeando os preliminares disponíveis — é a etapa, logo abaixo. A memoização
                // por código evita reler o catálogo quando o produto acabou de ser resolvido;
                // quando ele já era declarado, esta é a única leitura que o confere.
                if (produtoAncora is not null
                    && await ResolverTipoDeAtoAsync(produtoAncora.AtoCodigo).ConfigureAwait(false) is { } tipoAncora)
                {
                    if (tipoAncora.CongelaConfiguracao)
                    {
                        unitOfWork.DescartarAlteracoesNaoSalvas();
                        return Result<MutacaoAceita>.Failure(new DomainError(
                            "RecursoDaEtapa.AncoraEmAtoCongelante",
                            $"O tipo de ato âncora '{tipoAncora.Codigo}', declarado pela etapa '{input.Nome}', congela configuração — a âncora do recurso nunca é o ato que congela a configuração."));
                    }

                    if (tipoAncora.EfeitoIrreversivel)
                    {
                        unitOfWork.DescartarAlteracoesNaoSalvas();
                        return Result<MutacaoAceita>.Failure(new DomainError(
                            "RecursoDaEtapa.AncoraEmAtoIrreversivel",
                            $"O tipo de ato âncora '{tipoAncora.Codigo}', declarado pela etapa '{input.Nome}', tem efeito irreversível — não cabe recurso contra ele, e sim contra o resultado que o fundamenta."));
                    }
                }

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
