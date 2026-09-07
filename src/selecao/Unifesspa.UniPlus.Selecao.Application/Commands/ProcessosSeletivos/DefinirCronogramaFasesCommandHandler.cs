namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Handler do <see cref="DefinirCronogramaFasesCommand"/> (Story #851, CA-06):
/// resolve os snapshots-copy de <c>FaseCanonica</c>/<c>TipoBanca</c> (módulo
/// Configuração), o grafo de precedências vigente e o tipo de cada ato publicado e do ato
/// âncora (módulo Publicações), usando a data do relógio injetado lida <b>uma vez</b> por
/// operação (ADR-0068) — e delega a montagem/validação ao domínio.
/// </summary>
/// <remarks>
/// ADR-0125: propaga TODOS os erros de <see cref="FaseCronograma.Criar"/> (não só o
/// primeiro), prefixados com <c>fases[i]</c> (erro sem field próprio) ou
/// <c>fases[i].campo</c>, e acumula junto as recusas de <b>produto</b> de todas as fases —
/// uma coleção mal declarada erra em vários itens ao mesmo tempo, e devolver só o primeiro
/// faria o operador descobrir os demais numa sequência de tentativas. Diferente das demais
/// fatias da rolagem, não há uma passada pura pré-I/O aqui — <c>OrigemData</c> é
/// snapshot-copy do cadastro (<see cref="IFaseCanonicaReader"/>) e o papel de cada produto
/// depende do catálogo de tipos de ato, não de campo cru do payload. Ordem permanece só
/// <c>throw</c> no domínio (nunca acumulada) e continua coberta pelo FluentValidation; a
/// janela (Fim ≥ Início) foi retirada do validator de propósito — ela JÁ acumula no domínio
/// (<c>FaseCronograma.JanelaInvertida</c>), e deixá-la também no validator faria o
/// middleware bloquear sozinho, sem nunca deixar o domínio acumulá-la junto de outras
/// violações da mesma fase.
/// <para>
/// As resoluções cross-módulo que não descrevem um item da coleção — fase canônica, tipo de
/// banca, regra do catálogo, âncora — continuam recusando na primeira: cada uma nomeia um
/// insumo que falta inteiro, e prosseguir a partir dela produziria diagnóstico sobre um
/// estado que não existe. Elas <b>levam junto</b> o que já foi acumulado, porém: parar é
/// uma decisão sobre o que ainda dá para avaliar, não licença para apagar defeito já
/// diagnosticado.
/// </para>
/// <para>
/// A recusa do domínio também sai como LOTE, não como primeiro erro: a conclusão do ciclo
/// recursal acumula uma violação por fase mal declarada, e reduzi-la a um
/// <c>DomainError</c> apagaria os campos que localizam cada uma.
/// </para>
/// </remarks>
public static class DefinirCronogramaFasesCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirCronogramaFasesCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFaseCanonicaReader faseCanonicaReader,
        ITipoBancaReader tipoBancaReader,
        IPrecedenciaFaseReader precedenciaFaseReader,
        IRegraCatalogoReader regraCatalogoReader,
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(faseCanonicaReader);
        ArgumentNullException.ThrowIfNull(tipoBancaReader);
        ArgumentNullException.ThrowIfNull(precedenciaFaseReader);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(tipoAtoPublicadoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição precede a resolução de regras externas (ADR-0110 D9) — ver a
        // mesma nota nos demais Definir*.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        // UMA leitura do relógio para toda a operação (ADR-0068): a vigência do ato
        // produzido e a do ato âncora são resolvidas contra o MESMO instante.
        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Memoiza a resolução por código dentro da execução do comando: o número de
        // produtos cresce com o cronograma inteiro, e sem isto uma fase que publica
        // preliminar e definitiva do mesmo certame releria o catálogo a cada item.
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

        List<FaseCronograma> fases = [];
        List<FieldError> recusasAcumuladas = [];

        // Parar na primeira resolução cross-módulo é deliberado — cada uma nomeia um insumo
        // que falta INTEIRO, e prosseguir a partir dela produziria diagnóstico sobre um
        // estado que não existe. Descartar o que já foi diagnosticado, não: as recusas de
        // produto acumuladas até aqui descrevem defeitos reais do payload, e o operador
        // precisa delas na mesma resposta.
        //
        // O erro que interrompe entra no lote COM CAMPO. Quando ele acompanha recusas que
        // têm campo, `errors[]` é emitido, e o array é montado sobre todos os itens: um
        // FieldError sem campo sairia no wire como "field": null, que a ADR-0023 não admite
        // — cada elemento de errors[] declara o caminho dot-notation do que falhou. Quando
        // não houver campo específico defensável, o âncora é a própria fase, mesmo fallback
        // que a propagação dos erros da fábrica aplica logo abaixo.
        Result<MutacaoAceita> InterromperCom(string campo, DomainError erro) =>
            Result<MutacaoAceita>.ValidationFailure([.. recusasAcumuladas, new FieldError(campo, erro)]);

        for (int indice = 0; indice < command.Fases.Count; indice++)
        {
            FaseCronogramaInput input = command.Fases[indice];
            FaseCanonicaView? faseCanonica = await faseCanonicaReader
                .ObterPorIdAsync(input.FaseCanonicaId, cancellationToken)
                .ConfigureAwait(false);
            if (faseCanonica is null)
            {
                return InterromperCom($"fases[{indice}].faseCanonicaId", new DomainError(
                    "FaseCronograma.FaseCanonicaNaoEncontrada",
                    $"Fase canônica {input.FaseCanonicaId} não encontrada ou não está mais viva."));
            }

            OrigemDataFase origemData = OrigemDataFaseCodigo.FromCodigo(faseCanonica.OrigemData);

            List<BancaRequerida> bancas = [];
            foreach (Guid tipoBancaId in input.TiposBancaIds)
            {
                TipoBancaView? tipoBanca = await tipoBancaReader
                    .ObterPorIdAsync(tipoBancaId, cancellationToken)
                    .ConfigureAwait(false);
                if (tipoBanca is null)
                {
                    return InterromperCom($"fases[{indice}].tiposBancaIds", new DomainError(
                        "FaseCronograma.TipoBancaNaoEncontrado",
                        $"Tipo de banca {tipoBancaId} não encontrado ou não está mais vivo."));
                }

                bancas.Add(BancaRequerida.Criar(tipoBanca.Id, tipoBanca.Codigo));
            }

            // Os produtos da fase: cada código resolvido contra o catálogo vigente, e o
            // papel aceito só onde o catálogo diz que o ato É resultado (ADR-0056 — a
            // classificação é intrínseca ao tipo e nunca varia por edital). A
            // irreversibilidade é LIDA para decidir a âncora do recurso, logo abaixo, e
            // descartada: ela evolui com o cadastro e não é copiada para a fase.
            List<ProdutoDaFase> produtos = [];
            bool produtosIntegros = true;
            for (int posicao = 0; posicao < input.Produtos.Count; posicao++)
            {
                ProdutoDaFaseInput produtoInput = input.Produtos[posicao];
                string campo = $"fases[{indice}].produtos[{posicao}]";

                if (!PapelProdutoFaseCodigo.TentarConverter(produtoInput.Papel, out PapelProdutoFase? papel))
                {
                    recusasAcumuladas.Add(new($"{campo}.papel", new DomainError(
                        "ProdutoDaFase.PapelDesconhecido",
                        $"O papel '{produtoInput.Papel}' não é declarável — use '{PapelProdutoFaseCodigo.Preliminar}', '{PapelProdutoFaseCodigo.Definitivo}' ou nenhum.")));
                    produtosIntegros = false;
                    continue;
                }

                TipoAtoPublicadoView? tipoAto = await ResolverTipoDeAtoAsync(produtoInput.AtoCodigo).ConfigureAwait(false);
                if (tipoAto is null)
                {
                    recusasAcumuladas.Add(new($"{campo}.atoCodigo", new DomainError(
                        "ProdutoDaFase.AtoNaoEncontradoNoCatalogo",
                        $"O tipo de ato '{produtoInput.AtoCodigo}' não tem versão vigente no catálogo de Publicações na data de hoje.")));
                    produtosIntegros = false;
                    continue;
                }

                if (papel is not null && !tipoAto.EhResultado)
                {
                    recusasAcumuladas.Add(new($"{campo}.papel", new DomainError(
                        "ProdutoDaFase.PapelEmAtoQueNaoEhResultado",
                        $"O tipo de ato '{produtoInput.AtoCodigo}' não é resultado no catálogo — só resultado recebe papel preliminar ou definitivo.")));
                    produtosIntegros = false;
                    continue;
                }

                produtos.Add(ProdutoDaFase.Criar(tipoAto.Codigo, papel));
            }

            RegraRecursoFase? regraRecurso = null;
            if (input.RegraRecurso is { } regraInput)
            {
                // CA-01/CA-02/D9: a regra referenciada é resolvida via o MESMO
                // IRegraCatalogoReader que DefinirClassificacaoCommandHandler/
                // DefinirBonusRegionalCommandHandler usam — nunca leitura própria.
                RegraCatalogo? regraCatalogo = await regraCatalogoReader
                    .ObterAsync(regraInput.RegraCodigo, regraInput.RegraVersao, cancellationToken)
                    .ConfigureAwait(false);
                if (regraCatalogo is null
                    || regraCatalogo.Tipo != TipoRegra.RegraPrazoRecurso
                    || regraCatalogo.Codigo != RegraPrazoRecursoCodigo.AncoradoEmAto)
                {
                    return InterromperCom($"fases[{indice}].regraRecurso.regraCodigo", new DomainError(
                        "RegraRecursoFase.RegraCatalogoInvalida",
                        $"A regra {regraInput.RegraCodigo}/{regraInput.RegraVersao} não é a regra {RegraPrazoRecursoCodigo.AncoradoEmAto} do tipo regra_prazo_recurso."));
                }

                // A referência é montada a partir dos valores RESOLVIDOS do catálogo — nunca
                // ecoados do payload —, o que já garante por construção que o hash bate com
                // o hash declarado (o 4º item da conferência do §"Resolução da regra
                // referenciada").
                Result<ReferenciaRegra> referenciaResult = ReferenciaRegra.Criar(
                    regraCatalogo.Codigo, regraCatalogo.Versao, regraCatalogo.Hash);
                if (referenciaResult.IsFailure)
                {
                    // A referência é montada a partir dos valores RESOLVIDOS do catálogo,
                    // não de campo do payload — o âncora é o bloco que a referenciou.
                    return InterromperCom($"fases[{indice}].regraRecurso", referenciaResult.Error!);
                }

                ArgsRegraPrazoRecurso args = new(
                    regraInput.PrazoValor,
                    regraInput.PrazoUnidade,
                    regraInput.AtoAncoraCodigo,
                    regraInput.SuspensividadePrimeiraInstanciaValor,
                    regraInput.SuspensividadePrimeiraInstanciaUnidade,
                    regraInput.SuspensividadeSegundaInstanciaValor,
                    regraInput.SuspensividadeSegundaInstanciaUnidade);

                Result<RegraRecursoFase> regraRecursoResult = RegraRecursoFase.Criar(referenciaResult.Value!, args);
                if (regraRecursoResult.IsFailure)
                {
                    // A factory acusa prazo, unidade ou par de suspensividade sem devolver
                    // campo próprio; o bloco inteiro da regra é o âncora que resta.
                    return InterromperCom($"fases[{indice}].regraRecurso", regraRecursoResult.Error!);
                }

                // Item 3/4 do §3.6: o ato âncora existe, está vigente, NÃO é congelante e
                // NÃO tem efeito irreversível. A memoização acima já devolve a resolução
                // feita para o produto homônimo, que é o caso normal — o domínio exige que
                // a âncora seja um dos produtos da própria fase.
                TipoAtoPublicadoView? tipoAncora = await ResolverTipoDeAtoAsync(args.AtoAncoraCodigo).ConfigureAwait(false);

                if (tipoAncora is null)
                {
                    return InterromperCom($"fases[{indice}].regraRecurso.atoAncoraCodigo", new DomainError(
                        "RegraRecursoFase.AncoraNaoEncontradaNoCatalogo",
                        $"O tipo de ato âncora '{args.AtoAncoraCodigo}' não tem versão vigente no catálogo de Publicações na data de hoje."));
                }

                if (tipoAncora.CongelaConfiguracao)
                {
                    return InterromperCom($"fases[{indice}].regraRecurso.atoAncoraCodigo", new DomainError(
                        "RegraRecursoFase.AncoraEmAtoCongelante",
                        $"O tipo de ato âncora '{args.AtoAncoraCodigo}' congela configuração — a âncora do recurso nunca é o ato que congela a configuração."));
                }

                // A irreversibilidade é resolvida do catálogo, não de cópia congelada na
                // fase (UNI-REQ-0080): um ato que consome vaga escassa concede um direito
                // que não se desfaz, e recurso contra ele não teria o que reverter. O
                // recurso cabível é contra o resultado que fundamentou o ato.
                if (tipoAncora.EfeitoIrreversivel)
                {
                    return InterromperCom($"fases[{indice}].regraRecurso.atoAncoraCodigo", new DomainError(
                        "RegraRecursoFase.AncoraEmAtoIrreversivel",
                        $"O tipo de ato âncora '{args.AtoAncoraCodigo}' tem efeito irreversível — não cabe recurso contra ele, e sim contra o resultado que o fundamenta."));
                }

                regraRecurso = regraRecursoResult.Value!;
            }

            // Sem os produtos íntegros não há o que a factory possa avaliar: produzResultado
            // deriva deles, e chamá-la com a coleção incompleta reportaria violações
            // inventadas pela própria falha anterior.
            if (!produtosIntegros)
            {
                continue;
            }

            Result<FaseCronograma> faseResult = FaseCronograma.Criar(
                input.Ordem,
                faseCanonica.Id,
                faseCanonica.Codigo,
                faseCanonica.DonoTipico,
                origemData,
                faseCanonica.AgrupaEtapas,
                faseCanonica.PermiteComplementacao,
                faseCanonica.ColetaInscricao,
                faseCanonica.ColetaSolicitacaoIsencao,
                input.Inicio,
                input.Fim,
                produtos,
                input.FaseConcluinteCodigo,
                input.EmiteParecerIndividual,
                bancas,
                regraRecurso);
            if (faseResult.IsFailure)
            {
                recusasAcumuladas.AddRange(faseResult.Errors
                    .Select(erro => erro with
                    {
                        Field = erro.Field is null ? $"fases[{indice}]" : $"fases[{indice}].{erro.Field}",
                    }));
                continue;
            }

            fases.Add(faseResult.Value!);
        }

        if (recusasAcumuladas.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(recusasAcumuladas);
        }

        // §3.3: o grafo de precedências é parâmetro, não navegação (ADR-0042) — resolvido
        // aqui e passado pronto ao domínio.
        IReadOnlyList<PrecedenciaFaseView> arestasVivas = await precedenciaFaseReader
            .ListarVivasAsync(cancellationToken)
            .ConfigureAwait(false);
        List<ArestaPrecedencia> precedencias = [.. arestasVivas
            .Select(static a => new ArestaPrecedencia(a.AntecessoraCodigo, a.SucessoraCodigo, a.PermiteSobreposicao))];

        Result definirResult = processo.DefinirCronogramaFases(fases, precedencias, command.Precondicao);
        if (definirResult.IsFailure)
        {
            // Propaga o LOTE, não o primeiro. A conclusão do ciclo recursal acumula uma
            // violação por fase mal declarada, cada uma com o campo que a localiza; reduzir
            // tudo a um DomainError apagaria os campos, e a resposta sairia sem `errors[]` —
            // o operador corrigiria a primeira fase, reenviaria, e só então descobriria a
            // segunda. A forma serve igualmente às recusas de erro único do mesmo método,
            // que continuam saindo com um item só.
            return Result<MutacaoAceita>.ValidationFailure(definirResult.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
