namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Enums;
using Events;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Agregado-raiz do certame (UNI-REQ-0014/0015): o administrador cria o
/// processo em rascunho e monta a configuração a partir dos cadastros de
/// referência. Todas as entidades de configuração pendem desta raiz e são
/// acessadas e persistidas exclusivamente por ela (repositório único
/// <c>IProcessoSeletivoRepository</c>).
/// </summary>
/// <remarks>
/// <para>
/// F0 entregou a raiz, as etapas pontuadas e a oferta de atendimento
/// especializado; F2 a distribuição de vagas; F3 o bônus regional (RN05) e os
/// critérios de desempate — ambos por referência ao catálogo de regras
/// tipadas (<c>rol_de_regras</c>, Story #772), nunca escalares crus. A
/// classificação (bloco 15º) entra na F4, compondo por referência as
/// dimensões já modeladas.
/// </para>
/// <para>
/// O documento normativo não pertence a esta raiz: ele é o <b>ato publicado</b>,
/// e vive no módulo <c>Publicacoes</c> (ADR-0103/0105). O que a publicação
/// produz aqui é a <see cref="VersaoConfiguracao"/> congelada (RN08), que
/// referencia o ato por VALOR — o par <c>{id, hash}</c>, sem chave estrangeira
/// (ADR-0061). Enquanto o processo está em rascunho, a configuração é livremente
/// substituível (o comando <c>Definir*</c> troca a coleção inteira). A
/// configuração é CRUD puro via EF Core — a fronteira de Event Sourcing
/// (ADR-0069) começa nos agregados de decisão downstream, nunca aqui.
/// </para>
/// </remarks>
public sealed class ProcessoSeletivo : SoftDeletableEntity
{
    public string Nome { get; private set; } = string.Empty;
    public TipoProcesso Tipo { get; private set; }
    public StatusProcesso Status { get; private set; }

    /// <summary>
    /// De onde vêm os candidatos deste certame (§3.4, Story #851) — NOT NULL, exigido na
    /// criação. Deriva o piso mínimo do cronograma de fases; nunca ramifica por
    /// <see cref="Tipo"/>.
    /// </summary>
    public OrigemCandidatos OrigemCandidatos { get; private set; }

    private readonly List<EtapaProcesso> _etapas = [];
    public IReadOnlyCollection<EtapaProcesso> Etapas => _etapas.AsReadOnly();

    /// <summary>Cronograma de fases do certame (1..*, Story #851) — o eixo temporal, distinto das <see cref="Etapas"/> (eixo de pontuação).</summary>
    private readonly List<FaseCronograma> _cronogramaFases = [];
    public IReadOnlyCollection<FaseCronograma> CronogramaFases => _cronogramaFases.AsReadOnly();

    /// <summary>Documentos exigidos do certame (0..*, Story #554) — por fase, aplicabilidade e gatilho.</summary>
    private readonly List<DocumentoExigido> _documentosExigidos = [];
    public IReadOnlyCollection<DocumentoExigido> DocumentosExigidos => _documentosExigidos.AsReadOnly();

    /// <summary>Âncora que resolve FAIXA_ETARIA na publicação (Story #554, PR #896) — ausência = nenhum gatilho por idade pode existir (bloqueado na publicação, ver <see cref="PendenciaDaReferenciaTemporalFatos"/>).</summary>
    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; private set; }

    public OfertaAtendimentoEspecializado? OfertaAtendimento { get; private set; }

    private readonly List<ConfiguracaoDistribuicaoVagas> _distribuicaoVagas = [];
    public IReadOnlyCollection<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas => _distribuicaoVagas.AsReadOnly();

    /// <summary>Bônus regional (RN05) — ausência = sem bônus (toggle por presença, INV-B5).</summary>
    public ConfiguracaoBonusRegional? BonusRegional { get; private set; }

    private readonly List<CriterioDesempate> _criteriosDesempate = [];
    public IReadOnlyCollection<CriterioDesempate> CriteriosDesempate => _criteriosDesempate.AsReadOnly();

    /// <summary>Configuração de classificação (15º bloco canônico, Story #775) — compõe por referência a fórmula, precisão, eliminação e ordem de alocação.</summary>
    public ConfiguracaoClassificacao? Classificacao { get; private set; }

    /// <summary>
    /// A sessão editorial aberta sobre a configuração — o <b>portador</b> da retificação
    /// (ADR-0110 D3). <see langword="null"/> quando não há retificação em curso.
    /// </summary>
    /// <remarks>
    /// É a <b>existência</b> dela que autoriza a mutação de um processo publicado — não um
    /// status. O <see cref="Status"/> continua <see cref="StatusProcesso.Publicado"/>
    /// durante toda a edição: o certame <b>está</b> publicado, e o candidato continua
    /// vendo a versão congelada vigente.
    /// <para>
    /// <b>Cuidado ao carregar:</b> <see langword="null"/> aqui significa tanto "não existe"
    /// quanto "não foi carregado". É por isso que a mutação tem carregamento próprio —
    /// <c>IProcessoSeletivoRepository.ObterParaMutacaoAsync</c>, o único que a inclui — e
    /// um fitness test que prova que todo handler de mutação passa por ele. Sem isso, um
    /// comando futuro que usasse um carregamento sem esta navegação recusaria uma edição
    /// legítima: fail-closed <b>indevido</b>.
    /// </para>
    /// </remarks>
    public RascunhoRetificacao? Rascunho { get; private set; }

    private ProcessoSeletivo() { }

    public static ProcessoSeletivo Criar(string nome, TipoProcesso tipo, OrigemCandidatos origemCandidatos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (tipo == TipoProcesso.Nenhum)
        {
            throw new ArgumentException("Tipo do processo é obrigatório.", nameof(tipo));
        }

        if (origemCandidatos == OrigemCandidatos.Nenhuma)
        {
            throw new ArgumentException("Origem dos candidatos é obrigatória.", nameof(origemCandidatos));
        }

        return new ProcessoSeletivo
        {
            Nome = nome.Trim(),
            Tipo = tipo,
            OrigemCandidatos = origemCandidatos,
            Status = StatusProcesso.Rascunho,
        };
    }

    /// <summary>
    /// Substitui integralmente as etapas pontuadas do processo. A ordem, o
    /// caráter e o peso definem o divisor da média
    /// (<see cref="CalcularDivisorMedia"/>).
    /// </summary>
    public Result DefinirEtapas(IReadOnlyList<EtapaProcesso> etapas, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        List<int> ordensInformadas = [.. etapas.Where(e => e.Ordem.HasValue).Select(e => e.Ordem!.Value)];
        if (ordensInformadas.Distinct().Count() != ordensInformadas.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.OrdemEtapaDuplicada",
                "Cada etapa deve ter uma ordem única dentro do processo."));
        }

        // §3.5 (Story #851, bicondicional fase×etapa): uma lista vazia é agora um estado
        // VÁLIDO — o processo sem prova (SiSU, classificação importada) não tem etapa. A
        // guarda "ao menos uma etapa compõe a nota" só vale QUANDO há etapas: sem ao menos
        // uma que componha a nota, CalcularDivisorMedia() retorna 0 — um processo só com
        // etapas eliminatórias (ou classificatórias sem peso) prepararia divisão por zero
        // na fórmula da nota final (NOTA FINAL = Soma(Etapa×peso) / divisor). O caminho de
        // lista vazia NÃO pula as guardas abaixo (desempate/eliminação órfãos) — elas
        // continuam valendo mesmo removendo todas as etapas.
        if (etapas.Count > 0 && !etapas.Any(e => e.ComponeNota))
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NenhumaEtapaComponeNota",
                "Ao menos uma etapa deve ter caráter classificatória ou ambas, com peso, para compor a nota final."));
        }

        // INV-B6 sobrevive a DefinirEtapas: um critério de desempate
        // DESEMPATE-MAIOR-NOTA-ETAPA já configurado referencia uma etapa pelo
        // Id (dentro do Args); trocar as etapas sem revalidar deixaria o
        // critério apontando para uma etapa removida — desempate inexecutável
        // (achado Codex). Rejeita a troca de etapas em vez de silenciosamente
        // invalidar o desempate; o admin reconfigura o desempate primeiro.
        List<Guid> novosIdsEtapas = [.. etapas.Select(e => e.Id)];
        IEnumerable<(CriterioDesempate Criterio, ArgsDesempateMaiorNotaEtapa Args)> criteriosPorEtapa =
            _criteriosDesempate
                .Where(c => c.Args is ArgsDesempateMaiorNotaEtapa)
                .Select(c => (Criterio: c, Args: (ArgsDesempateMaiorNotaEtapa)c.Args));
        foreach ((CriterioDesempate criterio, ArgsDesempateMaiorNotaEtapa args) in criteriosPorEtapa)
        {
            if (!novosIdsEtapas.Contains(args.EtapaRef))
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EtapaReferenciadaPorDesempate",
                    $"A etapa {args.EtapaRef} é referenciada por um critério de desempate (ordem {criterio.Ordem}) e não pode ser removida sem antes reconfigurar o desempate."));
            }
        }

        // INV-B4 sobrevive à reconfiguração de etapas: uma classificação já
        // definida referenciando ELIM-NOTA-MINIMA-ETAPA não pode ficar órfã
        // se a etapa referenciada for removida (mesma proteção do INV-B6
        // para critérios de desempate).
        if (Classificacao is not null)
        {
            ArgsElimNotaMinimaEtapa? eliminacaoOrfa = Classificacao.RegrasEliminacao
                .Select(r => r.Args)
                .OfType<ArgsElimNotaMinimaEtapa>()
                .FirstOrDefault(args => !novosIdsEtapas.Contains(args.EtapaRef));
            if (eliminacaoOrfa is not null)
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EtapaReferenciadaPorClassificacao",
                    $"A etapa {eliminacaoOrfa.EtapaRef} é referenciada por uma regra de eliminação da classificação e não pode ser removida sem antes reconfigurar a classificação."));
            }
        }

        _etapas.Clear();
        foreach (EtapaProcesso etapa in etapas)
        {
            etapa.VincularProcesso(Id);
            _etapas.Add(etapa);
        }

        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Define (ou substitui) a oferta de atendimento especializado do processo.
    /// A invariante ADR-0067 (tipo de deficiência só sob condição PcD) já foi
    /// garantida na montagem da oferta
    /// (<see cref="OfertaAtendimentoEspecializado.Criar"/>).
    /// </summary>
    public Result DefinirOfertaAtendimento(OfertaAtendimentoEspecializado oferta, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(oferta);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        // Story #554/issue #892 (achado Codex P2, PR #896, CA-03): um gatilho DNF com fato
        // CONDICAO_ATENDIMENTO referencia um código de condição por VALOR (não por Guid —
        // diferente da fase), então a checagem é precisa: só recusa se o novo conjunto de
        // ofertas realmente deixaria de conter um código hoje referenciado por condição
        // viva — redefinir preservando (ou ampliando) os códigos ofertados é aceito.
        HashSet<string> novosCodigos = [.. oferta.Condicoes.Select(c => c.CondicaoCodigo)];
        if (ReferenciaDinamicaSeriaInvalidada("CONDICAO_ATENDIMENTO", novosCodigos))
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.CondicaoAtendimentoReferenciadaPorExigenciaViva",
                "Existe condição de gatilho documental referenciando um código de condição de atendimento que deixaria de ser ofertado — ajuste ou remova a condição antes de redefinir a oferta de atendimento."));
        }

        oferta.VincularProcesso(Id);
        OfertaAtendimento = oferta;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente a distribuição de vagas do processo (Story
    /// #773, modelagem P-A): uma <see cref="ConfiguracaoDistribuicaoVagas"/>
    /// por oferta de curso, sem repetir a mesma oferta duas vezes. As
    /// invariantes de cada configuração (PR, referência demográfica,
    /// modalidades federais) já foram validadas em
    /// <see cref="ConfiguracaoDistribuicaoVagas.Criar"/>.
    /// </summary>
    public Result DefinirDistribuicaoVagas(IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (distribuicaoVagas.Count == 0)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.DistribuicaoVagasVazia",
                "O processo deve ter ao menos uma distribuição de vagas configurada."));
        }

        List<Guid> ofertasInformadas = [.. distribuicaoVagas.Select(d => d.OfertaCursoOrigemId)];
        if (ofertasInformadas.Distinct().Count() != ofertasInformadas.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.OfertaCursoDuplicada",
                "Cada oferta de curso só pode ter uma distribuição de vagas no processo."));
        }

        // issue #848/ADR-0115 §3.7: o mesmo código de modalidade não pode ter ações
        // divergentes de vaga quando indeferido em ofertas distintas do processo —
        // AcaoQuandoIndeferido já existe em ModalidadeSelecionada e já é congelado no
        // bloco 'modalidades'; este guard só garante consistência entre ofertas, sem
        // duplicar o campo em VagaOfertada. Compartilhado com ValidarGrafo — a
        // restauração de envelope congelado reconstrói _distribuicaoVagas via
        // AplicarGrafo, não por este método, e precisa da mesma checagem.
        if (HaAcaoQuandoIndeferidoDivergenteEntreOfertas(distribuicaoVagas))
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.AcaoQuandoIndeferidoDivergente",
                "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo."));
        }

        // Story #554/issue #892 (achado Codex P2, PR #896, CA-03): mesmo raciocínio do
        // guard de CONDICAO_ATENDIMENTO em DefinirOfertaAtendimento — MODALIDADE referencia
        // por código, então a checagem é precisa (só recusa se um código hoje referenciado
        // por condição viva deixaria de existir na nova distribuição).
        HashSet<string> novosCodigos = [.. distribuicaoVagas.SelectMany(static d => d.Modalidades).Select(static m => m.Codigo)];
        if (ReferenciaDinamicaSeriaInvalidada("MODALIDADE", novosCodigos))
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.ModalidadeReferenciadaPorExigenciaViva",
                "Existe condição de gatilho documental referenciando um código de modalidade que deixaria de ser ofertado — ajuste ou remova a condição antes de redefinir a distribuição de vagas."));
        }

        _distribuicaoVagas.Clear();
        foreach (ConfiguracaoDistribuicaoVagas configuracao in distribuicaoVagas)
        {
            configuracao.VincularProcesso(Id);
            _distribuicaoVagas.Add(configuracao);
        }

        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Define (ou remove) o bônus regional do processo (RN05). Passar
    /// <see langword="null"/> remove o bônus — a ausência da entidade já é o
    /// toggle "sem bônus" (INV-B5); não existe um "BONUS-NENHUM".
    /// </summary>
    public Result DefinirBonusRegional(ConfiguracaoBonusRegional? bonus, PrecondicaoIfMatch precondicao)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (bonus is null)
        {
            BonusRegional = null;
            Rascunho?.IncrementarRevisao();
            return Result.Success();
        }

        bonus.VincularProcesso(Id);
        BonusRegional = bonus;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente os critérios de desempate do processo (Story
    /// #774, modelagem P-B §2.6). Dimensão opcional (0..*): lista vazia
    /// remove todos os critérios. INV-B6: todo <c>etapa_ref</c> referenciado
    /// por um critério <c>DESEMPATE-MAIOR-NOTA-ETAPA</c> precisa existir entre
    /// as etapas deste processo — senão a config congelaria um desempate
    /// inexecutável.
    /// </summary>
    public Result DefinirCriteriosDesempate(IReadOnlyList<CriterioDesempate> criterios, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        List<int> ordensInformadas = [.. criterios.Select(c => c.Ordem)];
        if (ordensInformadas.Distinct().Count() != ordensInformadas.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.OrdemDesempateDuplicada",
                "Cada critério de desempate deve ter uma ordem única dentro do processo."));
        }

        foreach (CriterioDesempate criterio in criterios)
        {
            if (criterio.Args is not ArgsDesempateMaiorNotaEtapa args)
            {
                continue;
            }

            if (!_etapas.Any(e => e.Id == args.EtapaRef))
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EtapaRefDesempateInexistente",
                    $"O critério de desempate na ordem {criterio.Ordem} referencia a etapa {args.EtapaRef}, que não existe neste processo (INV-B6)."));
            }
        }

        _criteriosDesempate.Clear();
        foreach (CriterioDesempate criterio in criterios)
        {
            criterio.VincularProcesso(Id);
            _criteriosDesempate.Add(criterio);
        }

        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Define (ou substitui) a configuração de classificação do processo
    /// (Story #775, modelagem P-B §2.1). Valida as invariantes que dependem
    /// de OUTRAS dimensões do agregado: INV-B4 (todo <c>etapa_ref</c> de uma
    /// <c>ELIM-NOTA-MINIMA-ETAPA</c> deve existir entre as etapas do
    /// processo) e a restrição de que <c>ELIM-CORTE-REDACAO</c>/
    /// <c>ELIM-ZERO-EM-AREA</c> só se aplicam a processo baseado em ENEM
    /// (INV-B13 parcial — SiSU/PSVR). As invariantes internas da própria
    /// configuração (INV-B8, limites de <c>NOpcoesAlocacao</c>) já foram
    /// validadas em <see cref="ConfiguracaoClassificacao.Criar"/>.
    /// </summary>
    public Result DefinirClassificacao(ConfiguracaoClassificacao classificacao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(classificacao);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        bool baseadoEmEnem = Tipo is TipoProcesso.SiSU or TipoProcesso.PSVR;

        foreach (RegraEliminacao regra in classificacao.RegrasEliminacao)
        {
            if (regra.Args is ArgsElimNotaMinimaEtapa notaMinima && !_etapas.Any(e => e.Id == notaMinima.EtapaRef))
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
                    $"A regra de eliminação referencia a etapa {notaMinima.EtapaRef}, que não existe neste processo (INV-B4)."));
            }

            bool somenteEnem = regra.Args is ArgsElimCorteRedacao or ArgsElimZeroEmArea;
            if (somenteEnem && !baseadoEmEnem)
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
                    $"A regra {regra.Regra.Codigo} só se aplica a processo baseado em ENEM (SiSU/PSVR)."));
            }
        }

        classificacao.VincularProcesso(Id);
        Classificacao = classificacao;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente o cronograma de fases do processo (Story #851, §3.7):
    /// mesmo padrão dos demais <c>Definir*</c> — <see cref="MutacaoBloqueada"/> primeiro,
    /// <see cref="Result"/> nunca exceção, substituição integral da coleção.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>O grafo de precedências é parâmetro, não navegação</b> (ADR-0042): o domínio
    /// nunca injeta <c>IPrecedenciaFaseReader</c> — o handler resolve o grafo vigente
    /// (módulo Configuração, cross-módulo) e o passa já pronto.
    /// </para>
    /// <para>
    /// Valida aqui o que só a raiz consegue provar (referências entre fases do MESMO
    /// cronograma e contra as etapas do MESMO processo): ordem/fase-canônica únicas
    /// (CA-06), a direção "fase de avaliação sem etapa" da bicondicional §3.5 (a
    /// direção "etapa sem fase" é lazy — só aflora no gate de publicação, porque uma
    /// etapa pode ser declarada DEPOIS do cronograma) e a precedência entre fases
    /// (§3.3, CA-08/CA-09) — <b>ausência de uma das duas fases de uma aresta não é
    /// violação</b> (contraprova CA-08).
    /// </para>
    /// </remarks>
    public Result DefinirCronogramaFases(
        IReadOnlyList<FaseCronograma> fases,
        IReadOnlyList<ArestaPrecedencia> precedencias,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(precedencias);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (fases.Count == 0)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.CronogramaFasesVazio",
                "O processo deve ter ao menos uma fase no cronograma."));
        }

        List<int> ordens = [.. fases.Select(f => f.Ordem)];
        if (ordens.Distinct().Count() != ordens.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.OrdemFaseDuplicada",
                "Cada fase deve ter uma ordem única dentro do cronograma."));
        }

        List<Guid> origens = [.. fases.Select(f => f.FaseCanonicaOrigemId)];
        if (origens.Distinct().Count() != origens.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.FaseCanonicaDuplicada",
                "A mesma fase canônica não pode aparecer duas vezes no cronograma."));
        }

        // Story #554/issue #893 (PR #900, CA-04): reconciliação por FaseCanonicaOrigemId —
        // achado Codex P2 (PR #900, 4ª rodada). FaseCanonicaOrigemId é a identidade ESTÁVEL
        // de uma fase no cronograma (índice único ux_fases_cronograma_processo_fase_canonica);
        // Ordem é só um ATRIBUTO mutável dela (uma fase pode ser reordenada sem deixar de
        // ser "a mesma fase"). Casar por Ordem (como as rodadas anteriores fizeram) confundia
        // "reordenar" com "trocar de fase": uma redefinição que só troca a POSIÇÃO de duas
        // fases já existentes (A@1,B@2 -> B@1,A@2) fazia a reconciliação RETARGETAR o
        // FaseCanonicaOrigemId das linhas rastreadas, e o EF não consegue ordenar as duas
        // instruções UPDATE resultantes (dependência circular: cada uma precisa que a outra
        // rode primeiro para liberar o valor único) — estourava InvalidOperationException no
        // SaveChanges, fora do Result pattern. Casar por identidade evita esse retargeting
        // desnecessário; o guard de permutação cíclica de Ordem (abaixo) cobre o caso
        // residual em que a PRÓPRIA Ordem trocada forma um ciclo fechado entre fases
        // retidas. Substitui o guard bruto da PR #895/#547 (que bloqueava QUALQUER redefinição
        // enquanto existisse exigência viva) por guards precisos: só recusa quando uma fase
        // REALMENTE removida é referenciada por exigência viva (ExigidoNaFaseId ou
        // IdadeMaximaEmissao.ReferenciaFaseId), ou quando uma fase SOBREVIVENTE perde
        // PermiteComplementacao/o extremo âncora sendo referenciada.
        Dictionary<Guid, FaseCronograma> fasesAntigasPorOrigem = _cronogramaFases.ToDictionary(f => f.FaseCanonicaOrigemId);
        Dictionary<Guid, FaseCronograma> fasesNovasPorOrigem = fases.ToDictionary(f => f.FaseCanonicaOrigemId);

        foreach (FaseCronograma antiga in fasesAntigasPorOrigem.Values)
        {
            if (!fasesNovasPorOrigem.TryGetValue(antiga.FaseCanonicaOrigemId, out FaseCronograma? nova))
            {
                // A fase removida pode ser referenciada por ExigidoNaFaseId OU por
                // IdadeMaximaEmissao.ReferenciaFaseId (PR #900) — os dois são vínculos
                // independentes, e ambos ficariam órfãos silenciosamente.
                if (_documentosExigidos.Any(d => d.ExigidoNaFaseId == antiga.Id
                    || d.IdadeMaximaEmissao?.ReferenciaFaseId == antiga.Id))
                {
                    return Result.Failure(new DomainError(
                        "FaseCronograma.ReferenciadaPorExigenciaViva",
                        $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) está sendo removida do cronograma, mas é referenciada por um documento exigido configurado."));
                }

                continue;
            }

            bool perdeuPermiteComplementacao = antiga.PermiteComplementacao && !nova.PermiteComplementacao;
            if (perdeuPermiteComplementacao
                && _documentosExigidos.Any(d => d.ExigidoNaFaseId == antiga.Id && d.ConsequenciaIndeferimento == "PENDENCIA_REENVIO"))
            {
                return Result.Failure(new DomainError(
                    "FaseCronograma.PendenciaReenvioExigeComplementacao",
                    $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) não pode perder PermiteComplementacao — é referenciada por um documento exigido com consequência PENDENCIA_REENVIO."));
            }

            // Achado Codex P2 (PR #900): fase SOBREVIVENTE (mesma Ordem) cujo extremo
            // âncora (Início/Fim) deixa de existir — a checagem eager de
            // DefinirDocumentosExigidos só prova a coerência no INSTANTE da escrita da
            // exigência; sem este guard, uma redefinição de cronograma POSTERIOR poderia
            // apagar o extremo e deixar IdadeMaximaEmissao apontando para uma âncora
            // irresolvível, silenciosamente. Mesma família de guard de
            // ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente (PR #896), mas aqui é
            // preventivo (na escrita do cronograma), não descoberto só na publicação.
            bool perdeuInicio = antiga.Inicio is not null && nova.Inicio is null;
            bool perdeuFim = antiga.Fim is not null && nova.Fim is null;
            if ((perdeuInicio || perdeuFim) && _documentosExigidos.Any(d =>
                d.IdadeMaximaEmissao is { ReferenciaFaseId: { } refFaseId } idade
                && refFaseId == antiga.Id
                && ((perdeuInicio && idade.ReferenciaTipo == ReferenciaTipoIdadeEmissao.InicioFase)
                    || (perdeuFim && idade.ReferenciaTipo == ReferenciaTipoIdadeEmissao.FimFase))))
            {
                return Result.Failure(new DomainError(
                    "IdadeMaximaEmissao.FaseExtremoAusente",
                    $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) não pode perder o extremo usado como âncora de idade máxima de emissão por um documento exigido configurado."));
            }
        }

        // Achado Codex P2 (PR #900, 5ª rodada): mesma lacuna do guard eager de
        // DefinirDocumentosExigidos — FIM_INSCRICAO não usa ReferenciaFaseId (a âncora é
        // implícita: a fase com ColetaInscricao), então os guards por fase acima (que só
        // olham ReferenciaFaseId) nunca pegam uma redefinição que deixa de ter QUALQUER
        // fase de coleta com Fim definido, mesmo com exigência viva ancorada em
        // FIM_INSCRICAO. É uma checagem GLOBAL, não por fase — não importa QUAL fase
        // perdeu o papel, o que importa é se ainda sobra alguma no cronograma NOVO.
        if (_documentosExigidos.Any(d => d.IdadeMaximaEmissao?.ReferenciaTipo == ReferenciaTipoIdadeEmissao.FimInscricao)
            && !fases.Any(f => f.ColetaInscricao && f.Fim is not null))
        {
            return Result.Failure(new DomainError(
                "IdadeMaximaEmissao.FaseExtremoAusente",
                "A redefinição do cronograma deixaria de ter uma fase que coleta inscrição com Fim definido, mas há documento exigido configurado com idade máxima de emissão ancorada em FIM_INSCRICAO."));
        }

        // Achado Codex P2 (PR #900, 4ª rodada): mesmo casando por identidade estável, uma
        // PERMUTAÇÃO CÍCLICA de Ordem entre fases retidas (ex.: A@1,B@2 -> A@2,B@1, ou um
        // ciclo de 3+) ainda pede que linhas TROQUEM valores cobertos pelo índice único
        // ux_fases_cronograma_processo_ordem entre si — nenhuma ordem de UPDATE resolve um
        // ciclo fechado num único SaveChanges (cada linha depende da outra liberar o valor
        // primeiro). Detecta o ciclo em termos puramente de domínio (sem conhecer EF/SQL) e
        // recusa com um erro nomeado, em vez de deixar a exceção do EF escapar do Result
        // pattern. Uma cadeia que termina numa Ordem livre (nunca usada) ou na Ordem de uma
        // fase REMOVIDA (que libera a linha via DELETE) não é um ciclo — só o é quando a
        // cadeia volta a uma fase já visitada NA MESMA caminhada.
        Dictionary<int, Guid> origemAntigaPorOrdem = [];
        foreach (FaseCronograma antiga in fasesAntigasPorOrigem.Values.Where(f => fasesNovasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId)))
        {
            origemAntigaPorOrdem[antiga.Ordem] = antiga.FaseCanonicaOrigemId;
        }

        Dictionary<Guid, int> estadoDoNo = []; // 1 = na caminhada atual, 2 = concluído sem ciclo
        foreach (Guid origemInicial in fasesAntigasPorOrigem.Keys)
        {
            if (estadoDoNo.ContainsKey(origemInicial))
            {
                continue;
            }

            List<Guid> caminho = [];
            Guid? noAtual = origemInicial;
            bool cicloFechado = false;

            while (noAtual is { } no)
            {
                if (estadoDoNo.TryGetValue(no, out int estado))
                {
                    cicloFechado = estado == 1;
                    break;
                }

                if (!fasesNovasPorOrigem.TryGetValue(no, out FaseCronograma? nova)
                    || nova.Ordem == fasesAntigasPorOrigem[no].Ordem)
                {
                    break;
                }

                estadoDoNo[no] = 1;
                caminho.Add(no);
                noAtual = origemAntigaPorOrdem.TryGetValue(nova.Ordem, out Guid proximo) ? proximo : null;
            }

            foreach (Guid visitado in caminho)
            {
                estadoDoNo[visitado] = 2;
            }

            if (cicloFechado)
            {
                return Result.Failure(new DomainError(
                    "FaseCronograma.PermutacaoDeOrdemNaoSuportada",
                    "A redefinição do cronograma troca a Ordem entre fases já existentes formando um ciclo fechado (ex.: uma fase assume a Ordem de outra, que assume a Ordem da primeira) — isso não pode ser persistido em uma única chamada. Mova uma das fases para uma Ordem livre numa chamada separada antes de fechar o ciclo."));
            }
        }

        // §3.5 — direção "fase de avaliação sem etapa": bloqueante e IMEDIATA (a fase que
        // agrupa etapas existe se e somente se há etapa pontuada JÁ declarada no
        // processo). A direção inversa ("etapa sem fase de avaliação") é validada no
        // gate de publicação (PendenciaDoCronograma) — uma etapa pode ser declarada
        // depois do cronograma, e bloquear aqui recusaria uma ordem de montagem legítima.
        if (fases.Any(static f => f.AgrupaEtapas) && _etapas.Count == 0)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.AvaliacaoSemEtapa",
                "Uma fase que agrupa etapas foi declarada, mas o processo não tem nenhuma etapa pontuada."));
        }

        // §3.3 — precedência é dado de cadastro, não código: para toda aresta cujas DUAS
        // fases estão presentes no cronograma, Ordem(A) < Ordem(B); e, quando a aresta não
        // permite sobreposição e ambas têm janela, Fim(A) ≤ Inicio(B). A ausência de uma
        // das duas fases NÃO é violação (CA-08) — o `continue` abaixo é a prova disso.
        Dictionary<string, FaseCronograma> porCodigo = new(StringComparer.Ordinal);
        foreach (FaseCronograma fase in fases)
        {
            porCodigo[fase.Codigo] = fase;
        }

        foreach (ArestaPrecedencia aresta in precedencias)
        {
            if (!porCodigo.TryGetValue(aresta.AntecessoraCodigo, out FaseCronograma? antecessora)
                || !porCodigo.TryGetValue(aresta.SucessoraCodigo, out FaseCronograma? sucessora))
            {
                continue;
            }

            if (antecessora.Ordem >= sucessora.Ordem)
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.PrecedenciaFaseViolada",
                    $"A fase '{aresta.AntecessoraCodigo}' (ordem {antecessora.Ordem}) precede '{aresta.SucessoraCodigo}' (ordem {sucessora.Ordem}) — a ordem declarada viola a precedência do cadastro."));
            }

            if (!aresta.PermiteSobreposicao
                && antecessora.Fim is { } fimAntecessora
                && sucessora.Inicio is { } inicioSucessora
                && fimAntecessora > inicioSucessora)
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida",
                    $"A janela da fase '{aresta.SucessoraCodigo}' ({sucessora.Inicio:O}–{sucessora.Fim:O}) se sobrepõe à da fase '{aresta.AntecessoraCodigo}' ({antecessora.Inicio:O}–{antecessora.Fim:O}), e o cadastro não permite sobreposição entre elas."));
            }
        }

        // Reconciliação por FaseCanonicaOrigemId (achado Codex P2, PR #900, 4ª rodada) — a
        // mesma chave de identidade do guard acima. Reusa a instância TRACKED existente
        // (retargetando-a via AtualizarSnapshot, preservando o Id) sempre que a fase
        // canônica persiste no cronograma, independente de Ordem: evita tanto FK Restrict
        // de documentos_exigidos.exigido_na_fase_id quanto o retargeting desnecessário do
        // FaseCanonicaOrigemId que causava a colisão descrita acima — a essa altura, o
        // guard de permutação cíclica já garantiu que a Ordem final de cada linha retida
        // não fecha um ciclo com outra linha retida.
        List<FaseCronograma> resultantes = [];
        foreach (FaseCronograma nova in fases)
        {
            if (fasesAntigasPorOrigem.TryGetValue(nova.FaseCanonicaOrigemId, out FaseCronograma? existente))
            {
                existente.AtualizarSnapshot(
                    nova.FaseCanonicaOrigemId,
                    nova.Ordem,
                    nova.Codigo,
                    nova.DonoInstitucional,
                    nova.OrigemData,
                    nova.AgrupaEtapas,
                    nova.PermiteComplementacao,
                    nova.ProduzResultado,
                    nova.ResultadoDefinitivo,
                    nova.ColetaInscricao,
                    nova.Inicio,
                    nova.Fim,
                    nova.AtoProduzidoCodigo,
                    nova.AtoProduzidoEfeitoIrreversivel,
                    [.. nova.BancasRequeridas],
                    nova.RegraRecurso);
                resultantes.Add(existente);
            }
            else
            {
                resultantes.Add(nova);
            }
        }

        _cronogramaFases.Clear();
        foreach (FaseCronograma fase in resultantes)
        {
            fase.VincularProcesso(Id);
            _cronogramaFases.Add(fase);
        }

        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente a coleção de documentos exigidos do processo (Story #554,
    /// PR #895): mesmo padrão dos demais <c>Definir*</c> — <see cref="MutacaoBloqueada"/>
    /// primeiro, <see cref="Result"/> nunca exceção, substituição integral da coleção.
    /// </summary>
    /// <remarks>
    /// Valida aqui o que só a raiz consegue provar: cada <see cref="DocumentoExigido.ExigidoNaFaseId"/>
    /// referencia uma fase viva do cronograma do MESMO processo (§2 da issue #547). O
    /// gatilho DNF (<c>CondicaoGatilho</c>, PR #896), a base legal (PR #898) e a idade/formato/
    /// tamanho (PR #900) não são tocados aqui.
    /// </remarks>
    public Result DefinirDocumentosExigidos(
        IReadOnlyList<DocumentoExigido> itens,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(itens);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        foreach (DocumentoExigido item in itens)
        {
            if (!_cronogramaFases.Any(fase => fase.Id == item.ExigidoNaFaseId))
            {
                return Result.Failure(new DomainError(
                    "DocumentoExigido.FaseNaoPertenceAoProcesso",
                    $"A fase {item.ExigidoNaFaseId} não pertence ao cronograma deste processo."));
            }

            // Story #554/issue #893 (PR #900): âncora de fase de IdadeMaximaEmissao — mesma
            // família de checagem estrutural de ReferenciaTemporalFatos (PR #896), mas EAGER
            // (na escrita, não na publicação): a regra vive na exigência, e a exigência já
            // está sendo escrita agora — não há razão para adiar. Fase viva do MESMO
            // processo com o extremo correspondente não-nulo (sem fallback silencioso).
            if (item.IdadeMaximaEmissao is { ReferenciaFaseId: { } referenciaFaseId } idadeComFase)
            {
                FaseCronograma? faseAncora = _cronogramaFases.FirstOrDefault(f => f.Id == referenciaFaseId);
                if (faseAncora is null)
                {
                    return Result.Failure(new DomainError(
                        "IdadeMaximaEmissao.FaseNaoPertenceAoProcesso",
                        $"A fase âncora {referenciaFaseId} da idade máxima de emissão não pertence ao cronograma deste processo."));
                }

                DateTimeOffset? extremo = idadeComFase.ReferenciaTipo == ReferenciaTipoIdadeEmissao.InicioFase ? faseAncora.Inicio : faseAncora.Fim;
                if (extremo is null)
                {
                    return Result.Failure(new DomainError(
                        "IdadeMaximaEmissao.FaseExtremoAusente",
                        $"A fase âncora da idade máxima de emissão não tem {(idadeComFase.ReferenciaTipo == ReferenciaTipoIdadeEmissao.InicioFase ? "Início" : "Fim")} definido — sem fallback silencioso."));
                }
            }
            else if (item.IdadeMaximaEmissao is { ReferenciaTipo: ReferenciaTipoIdadeEmissao.FimInscricao })
            {
                // Achado Codex P2 (PR #900, 5ª rodada): FIM_INSCRICAO não usa
                // ReferenciaFaseId (a âncora é implícita — a fase com ColetaInscricao do
                // PRÓPRIO cronograma, não uma fase escolhida pelo chamador), então o
                // branch acima nunca a valida. Sem esta checagem, um PUT aceitava a regra
                // num processo sem NENHUMA fase que coleta inscrição (ex.: importação
                // externa) ou com a fase de coleta sem Fim definido, deixando-a
                // irresolvível depois — mesmo sem gate de publicação para idade de
                // emissão (issue #893 §1: é aviso, não bloqueio de presença).
                // Achado Codex P2 (PR #900, 6ª rodada): nada no domínio impede MAIS de uma
                // fase com ColetaInscricao — FirstOrDefault pegava a primeira, mesmo que
                // outra (com Fim definido) resolvesse a regra; um processo com duas fases
                // de coleta, a primeira sem Fim e a segunda com Fim, era um 422 falso. A
                // pergunta certa é existencial (Any), não posicional — a mesma forma já
                // usada pelo guard backward simétrico em DefinirCronogramaFases.
                if (!_cronogramaFases.Any(f => f.ColetaInscricao))
                {
                    return Result.Failure(new DomainError(
                        "IdadeMaximaEmissao.FaseNaoPertenceAoProcesso",
                        "FIM_INSCRICAO exige uma fase do cronograma com ColetaInscricao, e o processo não tem nenhuma."));
                }

                if (!_cronogramaFases.Any(f => f.ColetaInscricao && f.Fim is not null))
                {
                    return Result.Failure(new DomainError(
                        "IdadeMaximaEmissao.FaseExtremoAusente",
                        "Nenhuma fase que coleta inscrição tem Fim definido — FIM_INSCRICAO não pode ser resolvido, sem fallback silencioso."));
                }
            }
        }

        _documentosExigidos.Clear();
        foreach (DocumentoExigido item in itens)
        {
            item.VincularProcesso(Id);
            _documentosExigidos.Add(item);
        }

        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Define a política que ancora <c>FAIXA_ETARIA</c> na publicação (Story #554, PR #896 —
    /// B-03 do plano). Presença (0..1) — <see langword="null"/> é estado válido enquanto
    /// nenhuma exigência tem gatilho por idade; a ausência só vira pendência de publicação
    /// quando existir esse gatilho (<see cref="PendenciaDaReferenciaTemporalFatos"/>).
    /// </summary>
    public Result DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos? referencia, PrecondicaoIfMatch precondicao)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (referencia is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } and { FaseId: { } faseId }
            && !_cronogramaFases.Any(f => f.Id == faseId))
        {
            return Result.Failure(new DomainError(
                "ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso",
                $"A fase {faseId} não pertence ao cronograma deste processo."));
        }

        ReferenciaTemporalFatos = referencia;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Divisor da média da nota final: soma dos pesos das etapas que compõem
    /// a nota (caráter classificatória ou ambas, com peso declarado). Fórmula:
    /// <c>NOTA FINAL = Soma(Etapa × peso) / fator_de_divisão + bônus_regional</c>.
    /// </summary>
    public decimal CalcularDivisorMedia() =>
        _etapas.Where(e => e.ComponeNota).Sum(e => e.Peso!.Value);

    /// <summary>
    /// Aplicabilidade da concorrência dupla (Lei 14.723/2023, INV-B7):
    /// DERIVADA — <see langword="true"/> sse alguma modalidade selecionada em
    /// <see cref="DistribuicaoVagas"/> tem
    /// <see cref="NaturezaLegalModalidade.CotaReservada"/>. Nunca um toggle
    /// livre nem um campo persistido — computada sob demanda a partir do
    /// estado corrente para nunca dessincronizar se a distribuição de vagas
    /// mudar depois de a classificação ter sido configurada.
    /// </summary>
    public bool ConcorrenciaDuplaAplicavel() =>
        _distribuicaoVagas
            .SelectMany(d => d.Modalidades)
            .Any(m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada);

    /// <summary>
    /// Checklist de conformidade estrutural (Story #758 CA-07; reusado por
    /// <see cref="Publicar"/>, Story #759 CA-03) — cobre as dimensões
    /// estruturalmente OBRIGATÓRIAS do agregado: Oferta de atendimento
    /// especializado (1), Distribuição de vagas (1..*), Classificação (1) e
    /// Cronograma de fases (1..*, Story #851). Bônus regional (0..1) e
    /// critérios de desempate (0..*) são deliberadamente opcionais e NÃO
    /// entram — a ausência é um estado válido (RN05: ausência de bônus = sem
    /// bônus), não uma pendência. Única fonte de verdade do checklist: tanto
    /// <c>ObterConformidadeProcessoSeletivoQueryHandler</c> quanto
    /// <see cref="Publicar"/> chamam este método, nunca duplicam a lista.
    /// </summary>
    /// <remarks>
    /// <b>"Etapas" deixou de ser item incondicional (Story #851 §3.5).</b> Um processo
    /// sem prova (SiSU, <c>CLASSIFICACAO-IMPORTADA</c>) publica sem etapa — a fase que
    /// agrupa etapas existe se e somente se há etapa, e essa bicondicional é gate à
    /// parte (<see cref="PendenciaDoCronograma"/>), não item do checklist booleano. O
    /// que sobrevive aqui é a exigência de <see cref="RegraCalculoCodigo.FormulaMediaPonderada"/>:
    /// sob essa fórmula, o divisor da média (<see cref="CalcularDivisorMedia"/>) tem de
    /// ser maior que zero. Sob <see cref="RegraCalculoCodigo.ClassificacaoImportada"/>, a
    /// classificação dispensa etapa, fórmula e precisão locais — nenhum item aqui.
    /// </remarks>
    public IReadOnlyList<ItemConformidade> AvaliarConformidade()
    {
        List<ItemConformidade> itens =
        [
            new ItemConformidade("Atendimento especializado", OfertaAtendimento is not null),
            new ItemConformidade("Distribuição de vagas", _distribuicaoVagas.Count > 0),
            new ItemConformidade("Classificação", Classificacao is not null),
            new ItemConformidade("Cronograma de fases", _cronogramaFases.Count > 0),
            // Story #554, PR #898 (issue #549, ADR-0074): toda exigência que determina
            // resultado precisa de ≥1 base legal RESOLVIDO — semântica vazia quando não há
            // exigência que determine resultado (Services.ValidadorBaseLegalExigencias).
            new ItemConformidade(
                "Base legal das exigências documentais",
                Services.ValidadorBaseLegalExigencias.TodasResolvidas(_documentosExigidos)),
        ];

        if (Classificacao is { RegraCalculo.Codigo: RegraCalculoCodigo.FormulaMediaPonderada })
        {
            itens.Add(new ItemConformidade("Divisor da média (fórmula local)", CalcularDivisorMedia() > 0));
        }

        return itens;
    }

    /// <summary>
    /// Pendência de conformidade do processo, ou <see langword="null"/> quando
    /// ele está conforme. É a <b>fonte única</b> do gate: <see cref="Publicar"/>
    /// e <see cref="Retificar"/> chamam este método — não há segunda lista de
    /// itens em lugar nenhum, e as duas transições recusam com o <b>mesmo</b>
    /// <c>DomainError</c>.
    /// </summary>
    /// <remarks>
    /// Publicar e retificar abrem, ambos, uma <see cref="VersaoConfiguracao"/>
    /// append-only e juridicamente vinculante. Uma versão congelada a partir de
    /// configuração incompleta é irreparável — o passado não se muta. Por isso o
    /// checklist vale para as <b>duas</b> transições, não só para a primeira.
    /// </remarks>
    public DomainError? PendenciaDeConformidade()
    {
        IReadOnlyList<ItemConformidade> pendencias = [.. AvaliarConformidade().Where(static item => !item.Ok)];
        if (pendencias.Count == 0)
        {
            return null;
        }

        return new DomainError(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            $"Processo não conforme para publicação — pendente: {string.Join(", ", pendencias.Select(static p => p.Item))}.");
    }

    /// <summary>
    /// Pendências do cronograma que não cabem no checklist booleano de
    /// <see cref="AvaliarConformidade"/> — cada uma tem o seu próprio <c>DomainError</c>
    /// nomeado (Story #851 §3.4/§3.5, CA-11/CA-13/CA-14). Chamado por
    /// <see cref="Publicar"/> e por <see cref="SucederVersao"/> (Retificar/FecharRetificacao),
    /// sempre <b>depois</b> de <see cref="PendenciaDeConformidade"/>.
    /// </summary>
    private DomainError? PendenciaDoCronograma()
    {
        bool existeFaseDeAvaliacao = _cronogramaFases.Any(static f => f.AgrupaEtapas);

        // §3.5, direção "fase de avaliação sem etapa" — defesa em profundidade: o mesmo
        // sentido já é bloqueado eagerly em DefinirCronogramaFases, mas uma etapa
        // removida DEPOIS (via DefinirEtapas) deixaria uma fase de avaliação órfã sem
        // que nada a pegasse na hora — o gate de publicação é a rede de segurança.
        if (existeFaseDeAvaliacao && _etapas.Count == 0)
        {
            return new DomainError(
                "ProcessoSeletivo.AvaliacaoSemEtapa",
                "Há uma fase que agrupa etapas no cronograma, mas o processo não tem nenhuma etapa pontuada.");
        }

        // §3.5, direção "etapa sem fase de avaliação" — lazy por natureza (a etapa pode
        // ser declarada depois do cronograma).
        if (_etapas.Count > 0 && !existeFaseDeAvaliacao)
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
                "O processo tem etapa pontuada, mas nenhuma fase do cronograma agrupa etapas.");
        }

        // §3.4 — piso mínimo derivado da ORIGEM DOS CANDIDATOS, nunca do tipo.
        if (OrigemCandidatos == OrigemCandidatos.InscricaoPropria
            && !_cronogramaFases.Any(static f => f.ColetaInscricao))
        {
            return new DomainError(
                "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
                "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma coleta inscrição.");
        }

        // §3.4 — havendo vagas ofertadas, o cronograma precisa de ao menos uma fase que
        // produza resultado.
        bool temVagasOfertadas = _distribuicaoVagas.Any(static d => d.VoBase > 0);
        if (temVagasOfertadas && !_cronogramaFases.Any(static f => f.ProduzResultado))
        {
            return new DomainError(
                "ProcessoSeletivo.VagasSemFaseQueProduzResultado",
                "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado.");
        }

        return null;
    }

    /// <summary>
    /// Pendências dos documentos exigidos (Story #554). Chamado por
    /// <see cref="Publicar"/> e por <see cref="SucederVersao"/> (Retificar/
    /// FecharRetificacao), sempre <b>depois</b> de <see cref="PendenciaDoCronograma"/>.
    /// </summary>
    /// <remarks>
    /// <b>CA-01</b> (Story #554, issue #547) — uma exigência <c>CONDICIONAL</c> sem
    /// nenhuma condição de gatilho viva que <see cref="DocumentoExigido.DeterminaResultado"/>
    /// é "exigência morta": nunca seria cobrada de ninguém. Desde a PR #896 (issue #892),
    /// avalia a coleção REAL de <see cref="DocumentoExigido.Condicoes"/>.
    /// <para>
    /// A guarda fail-closed <b>B-01</b>, que bloqueava QUALQUER publicação com
    /// <see cref="DocumentoExigido"/> configurado enquanto o bloco
    /// <c>documentosExigidos.exigencias</c> do envelope era stub (PR #895..PR #900), foi
    /// <b>removida na PR #903</b> (issue #548): o bloco rico a substitui, e o gate real
    /// (<see cref="Services.AvaliadorConformidadeLegal.Avaliar"/>, predicado
    /// <c>DocumentoObrigatorioParaModalidade</c>) passa a decidir com base no que está
    /// realmente configurado, não mais reprovando por padrão conservador.
    /// </para>
    /// </remarks>
    private DomainError? PendenciaDasExigenciasDocumentais()
    {
        foreach (DocumentoExigido exigencia in _documentosExigidos)
        {
            bool possuiCondicaoViva = exigencia.Condicoes.Count > 0;
            if (exigencia.Aplicabilidade == Aplicabilidade.Condicional
                && !possuiCondicaoViva
                && exigencia.DeterminaResultado())
            {
                return new DomainError(
                    "DocumentoExigido.CondicionalVaziaDeterminaResultado",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' é CONDICIONAL, determina resultado, mas não tem nenhuma condição de gatilho viva — nunca seria cobrada de ninguém.");
            }
        }

        return null;
    }

    /// <summary>
    /// Coerência entre a consequência de indeferimento declarada em cada
    /// <see cref="DocumentoExigido"/> e a ação da vaga (Story #554, PR #903, CA-05) — lida
    /// de <see cref="ModalidadeSelecionada.AcaoQuandoIndeferido"/>, sem campo duplicado em
    /// <see cref="DocumentoExigido"/>. Chamado por <see cref="Publicar"/> e por
    /// <see cref="SucederVersao"/> (Retificar/FecharRetificacao), sempre <b>depois</b> de
    /// <see cref="PendenciaDasExigenciasDocumentais"/>. Sem cache — recomputado a cada
    /// chamada a partir da coleção real de <see cref="_documentosExigidos"/> e
    /// <see cref="_distribuicaoVagas"/>, então uma mudança de gatilho ou de distribuição
    /// de vagas entre versões é sempre reavaliada (contraprova "reavaliação após mudança
    /// de gatilho").
    /// </summary>
    /// <remarks>
    /// Duas checagens:
    /// <list type="bullet">
    /// <item><c>REMOVE_VANTAGEM</c> exige vantagem viva no processo — hoje a única
    /// vantagem modelada é <see cref="BonusRegional"/> (RN05, toggle por presença:
    /// ausência da entidade já significa sem bônus).</item>
    /// <item>Para cada modalidade que a exigência alcança (mesmo fato sintético
    /// <c>MODALIDADE</c> usado pelo gate real, <see cref="Services.AvaliadorConformidadeLegal"/>),
    /// quando essa modalidade declara <c>AcaoQuandoIndeferido</c>, a consequência precisa
    /// ser idêntica — mesmo vocabulário fechado dos dois lados.</item>
    /// </list>
    /// </remarks>
    private DomainError? PendenciaDeCoerenciaDaConsequenciaDeIndeferimento()
    {
        foreach (DocumentoExigido exigencia in _documentosExigidos)
        {
            if (exigencia.ConsequenciaIndeferimento is not { } consequencia)
            {
                continue;
            }

            if (consequencia == "REMOVE_VANTAGEM" && BonusRegional is null)
            {
                return new DomainError(
                    "DocumentoExigido.RemoveVantagemSemVantagemViva",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva (ex.: bônus regional) para remover.");
            }

            string consequenciaComoAcaoDaVaga = NormalizarConsequenciaParaAcaoDaVaga(consequencia);
            ModalidadeSelecionada? modalidadeIncoerente = ModalidadesAlcancadasPor(exigencia)
                .FirstOrDefault(modalidade => modalidade.AcaoQuandoIndeferido is { } acao
                    && !string.Equals(acao, consequenciaComoAcaoDaVaga, StringComparison.Ordinal));
            if (modalidadeIncoerente is not null)
            {
                return new DomainError(
                    "DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' declara consequência '{consequencia}', incoerente com a ação de indeferimento '{modalidadeIncoerente.AcaoQuandoIndeferido}' configurada para a modalidade '{modalidadeIncoerente.Codigo}'.");
            }
        }

        return null;
    }

    /// <summary>
    /// Ponte entre os dois vocabulários fechados de "ação de indeferimento" — Story #554
    /// (PR #903), achado de revisão: <see cref="DocumentoExigido.ConsequenciaIndeferimento"/>
    /// usa <c>RECLASSIFICA_AC</c> (rol fechado desde a PR #895, issue #547), mas
    /// <see cref="ModalidadeSelecionada.AcaoQuandoIndeferido"/> — snapshot-copy do cadastro
    /// de <c>Modalidade</c> no módulo Configuração
    /// (<c>ck_modalidade_acao_quando_indeferido</c>) — usa <c>RECLASSIFICAR_AC</c>. É o
    /// MESMO conceito com grafias diferentes, nunca unificadas entre os dois módulos;
    /// comparar os tokens crus reprovaria sempre o único caso de reclassificação
    /// realmente coerente que existe.
    /// </summary>
    private static string NormalizarConsequenciaParaAcaoDaVaga(string consequencia) =>
        consequencia == "RECLASSIFICA_AC" ? "RECLASSIFICAR_AC" : consequencia;

    /// <summary>
    /// Modalidades ofertadas cuja aplicabilidade da exigência cobre — mesmo fato sintético
    /// <c>MODALIDADE</c> avaliado por <see cref="DocumentoExigido.AplicavelPara"/> que o
    /// gate real usa (<see cref="Services.AvaliadorConformidadeLegal"/>): um fato por
    /// candidato hipotético dessa modalidade, nunca os fatos reais de um candidato.
    /// </summary>
    private IEnumerable<ModalidadeSelecionada> ModalidadesAlcancadasPor(DocumentoExigido exigencia) =>
        _distribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(modalidade => exigencia.AplicavelPara(new Dictionary<string, JsonElement>
            {
                ["MODALIDADE"] = JsonSerializer.SerializeToElement(modalidade.Codigo),
            }));

    /// <summary>
    /// Pendência de <see cref="ReferenciaTemporalFatos"/> (Story #554, PR #896 — B-03 do
    /// plano). Chamado por <see cref="Publicar"/> e por <see cref="SucederVersao"/>,
    /// sempre <b>depois</b> de <see cref="PendenciaDasExigenciasDocumentais"/>.
    /// </summary>
    /// <remarks>
    /// Sem fallback silencioso (ADR-0111:235-236): se existir gatilho por
    /// <c>FAIXA_ETARIA</c> em qualquer <see cref="DocumentoExigido"/>, a referência
    /// precisa resolver para uma data concreta — política ausente, âncora de fase sem o
    /// extremo escolhido, ou <c>FIM_INSCRICAO</c> sem fase que colete inscrição com
    /// <c>Fim</c> definido bloqueiam a publicação. O congelamento da <c>DateOnly</c>
    /// concreta é da PR #903; esta validação só prova que ela É resolvível.
    /// </remarks>
    private DomainError? PendenciaDaReferenciaTemporalFatos()
    {
        bool existeGatilhoPorFaixaEtaria = _documentosExigidos
            .SelectMany(static d => d.Condicoes)
            .Any(static c => string.Equals(c.Fato, "FAIXA_ETARIA", StringComparison.Ordinal));

        if (!existeGatilhoPorFaixaEtaria)
        {
            return null;
        }

        if (ReferenciaTemporalFatos is not { } referencia)
        {
            return new DomainError(
                "ProcessoSeletivo.ReferenciaTemporalFatosAusente",
                "Existe gatilho por FAIXA_ETARIA, mas nenhuma referência temporal de fatos foi configurada — a publicação não pode resolver a idade do candidato sem fallback silencioso (ADR-0111).");
        }

        if (referencia.Tipo is ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase)
        {
            FaseCronograma? fase = _cronogramaFases.FirstOrDefault(f => f.Id == referencia.FaseId);
            if (fase is null)
            {
                return new DomainError(
                    "ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente",
                    "A fase âncora da referência temporal de fatos não pertence (mais) ao cronograma deste processo.");
            }

            DateTimeOffset? extremo = referencia.Tipo == ReferenciaTipo.InicioFase ? fase.Inicio : fase.Fim;
            if (extremo is null)
            {
                return new DomainError(
                    "ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente",
                    $"A fase âncora da referência temporal de fatos não tem {(referencia.Tipo == ReferenciaTipo.InicioFase ? "Início" : "Fim")} definido — sem fallback silencioso.");
            }
        }
        else if (referencia.Tipo == ReferenciaTipo.FimInscricao)
        {
            // Achado de revisão (Story #554, PR #903): nada no domínio impede MAIS de uma
            // fase com ColetaInscricao (mesma família de guard já corrigida para
            // IdadeMaximaEmissao, PR #900) — FirstOrDefault pegava a primeira, mesmo que
            // outra (com Fim definido) resolvesse a referência. A pergunta certa é
            // existencial (Any), não posicional.
            if (!_cronogramaFases.Any(static f => f.ColetaInscricao && f.Fim is not null))
            {
                return new DomainError(
                    "ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel",
                    "FIM_INSCRICAO exige uma fase que colete inscrição com Fim definido — sem fallback silencioso.");
            }
        }

        // DATA_ESPECIFICA: ReferenciaTemporalFatos.Criar já garante Data presente — nada a checar aqui.
        return null;
    }

    /// <summary>
    /// Resolve <see cref="ReferenciaTemporalFatos"/> para a <see cref="DateOnly"/> concreta
    /// que o envelope congela como <c>dataReferenciaFatos</c> (Story #554, PR #903, B-03) — o
    /// mesmo par (Tipo, âncora) que <see cref="PendenciaDaReferenciaTemporalFatos"/> já
    /// provou resolvível antes da transição chamar este método; aqui só se resolve, sem
    /// revalidar. Fuso <c>America/Sao_Paulo</c> — a virada do dia UTC→local é a razão de
    /// existir deste método em vez de o encoder ler <see cref="DateTimeOffset"/> cru.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> quando não há gatilho por <c>FAIXA_ETARIA</c> — a resolução
    /// não foi provada nem é necessária, e congelar uma data não pedida por ninguém seria
    /// dado morto no envelope, não uma garantia a mais.
    /// </returns>
    public DateOnly? ResolverDataReferenciaFatos()
    {
        bool existeGatilhoPorFaixaEtaria = _documentosExigidos
            .SelectMany(static d => d.Condicoes)
            .Any(static c => string.Equals(c.Fato, "FAIXA_ETARIA", StringComparison.Ordinal));
        if (!existeGatilhoPorFaixaEtaria)
        {
            return null;
        }

        if (ReferenciaTemporalFatos is not { } referencia)
        {
            throw new InvalidOperationException(
                "Resolução de dataReferenciaFatos sem ReferenciaTemporalFatos configurada — PendenciaDaReferenciaTemporalFatos deveria ter recusado a transição antes deste ponto.");
        }

        if (referencia.Tipo == ReferenciaTipo.DataEspecifica)
        {
            return referencia.Data!.Value;
        }

        DateTimeOffset? extremo = referencia.Tipo switch
        {
            ReferenciaTipo.InicioFase => _cronogramaFases.FirstOrDefault(f => f.Id == referencia.FaseId)?.Inicio,
            ReferenciaTipo.FimFase => _cronogramaFases.FirstOrDefault(f => f.Id == referencia.FaseId)?.Fim,
            ReferenciaTipo.FimInscricao => _cronogramaFases.FirstOrDefault(static f => f.ColetaInscricao && f.Fim is not null)?.Fim,
            _ => throw new InvalidOperationException($"Tipo de ReferenciaTemporalFatos não reconhecido: {referencia.Tipo}."),
        };

        if (extremo is not { } instante)
        {
            throw new InvalidOperationException(
                "dataReferenciaFatos não resolvível a partir do cronograma — PendenciaDaReferenciaTemporalFatos deveria ter recusado a transição antes deste ponto.");
        }

        return DateOnly.FromDateTime(instante.ParaHorarioBrasilia().DateTime);
    }

    /// <summary>
    /// CA-03 (Story #554, issue #892 — achado Codex P2, PR #896): um gatilho DNF sobre um
    /// fato categórico dinâmico (<c>MODALIDADE</c>, <c>CONDICAO_ATENDIMENTO</c>) referencia
    /// um valor por CÓDIGO — nunca por Guid, diferente de <c>FaseCronograma</c>. Isso
    /// permite checagem precisa (não o guard coarse de <c>FaseCronograma.ReferenciadaPorExigenciaViva</c>):
    /// só invalida quando o novo conjunto de códigos ofertados realmente deixaria de conter
    /// um valor hoje referenciado por uma condição viva — redefinir preservando (ou
    /// ampliando) a oferta é sempre aceito.
    /// </summary>
    private bool ReferenciaDinamicaSeriaInvalidada(string fato, HashSet<string> novosCodigosValidos)
    {
        foreach (CondicaoGatilho condicao in _documentosExigidos.SelectMany(static d => d.Condicoes))
        {
            if (!string.Equals(condicao.Fato, fato, StringComparison.Ordinal))
            {
                continue;
            }

            IEnumerable<string?> valoresReferenciados = condicao.Valor.ValueKind == JsonValueKind.Array
                ? condicao.Valor.EnumerateArray().Select(static v => v.GetString())
                : [condicao.Valor.GetString()];

            if (valoresReferenciados.Any(valor => valor is not null && !novosCodigosValidos.Contains(valor)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Publica o processo (RN08, Story #759 T4): valida a transição e a
    /// conformidade estrutural, abre a cadeia de <see cref="VersaoConfiguracao"/>
    /// a partir dos bytes canônicos já produzidos pelo
    /// <c>ISnapshotPublicacaoCanonicalizer</c> (Application — Domain não pode
    /// chamá-lo, ver ADR-0042) e transita o status — tudo dentro deste método,
    /// atomicamente em memória; o handler só persiste o resultado numa única
    /// transação.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A raiz <b>decide o identificador</b> do ato que cria a versão, mas não o
    /// documento: o ato publicado vive no módulo <c>Publicacoes</c> (ADR-0103/0105) e
    /// é registrado a partir da mensagem durável que sai desta mesma transação
    /// (ADR-0108). É por isso que o id é decidido aqui e não lá — sem ele, a versão
    /// não teria o que referenciar, e a reentrega da fila (at-least-once) criaria um
    /// ato gêmeo em vez de reencontrar o mesmo.
    /// </para>
    /// <para>
    /// A referência é <b>por valor</b>: a versão guarda o par <c>{id, hash}</c> do ato,
    /// sem chave estrangeira (ADR-0061). Nada aqui sabe o que o ato <i>é</i> — o tipo
    /// vem do catálogo de Publicações, e retificar é uma relação entre atos, nunca um
    /// tipo (ADR-0103).
    /// </para>
    /// </remarks>
    /// <param name="dados">Número, período de inscrição e referência ao documento confirmado.</param>
    /// <param name="configuracaoCongeladaCanonica">Bytes canônicos (ADR-0100) já produzidos pelo canonicalizador da Application.</param>
    /// <param name="schemaVersion">Versão do conjunto de blocos do snapshot (ADR-0100 item 8).</param>
    /// <param name="algoritmoHash">Identificador do algoritmo de hash (ex.: <c>canonical-json/sha256@v1</c>).</param>
    /// <param name="hashDocumento">Hash SHA-256 do documento publicado (T3, #784) — o hash do ato criador.</param>
    /// <param name="atorUsuarioSub">Sub do usuário autenticado responsável pela publicação (via <c>IUserContext</c>, nunca input do command).</param>
    /// <param name="clock">Relógio injetado (ADR-0068) — nunca lido implicitamente.</param>
    public Result<VersaoConfiguracao> Publicar(
        DadosEdital dados,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashDocumento,
        string atorUsuarioSub,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(configuracaoCongeladaCanonica);
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != StatusProcesso.Rascunho)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível publicar um processo em rascunho — status atual: {Status}."));
        }

        if (PendenciaDeConformidade() is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        if (PendenciaDoCronograma() is { } pendenciaCronograma)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCronograma);
        }

        if (PendenciaDasExigenciasDocumentais() is { } pendenciaExigencias)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaExigencias);
        }

        if (PendenciaDeCoerenciaDaConsequenciaDeIndeferimento() is { } pendenciaCoerenciaConsequencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCoerenciaConsequencia);
        }

        if (PendenciaDaReferenciaTemporalFatos() is { } pendenciaReferenciaTemporal)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaReferenciaTemporal);
        }

        // UMA leitura do relógio para o ato e para a versão que ele cria. O instante
        // que o ProcessoPublicadoEvent publica é o do ato, de modo que resolver a
        // configuração NESSE instante (ADR-0075: o ato é avaliado contra o que vigia
        // quando ocorreu) tem de cair dentro da vigência da versão que o próprio ato
        // criou. Duas leituras deixariam a vigência alguns ticks à frente e aflorariam
        // VigenteAusente.
        DateTimeOffset instantePublicacao = clock.GetUtcNow();

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            Id,
            configuracaoCongeladaCanonica,
            schemaVersion,
            algoritmoHash,
            atoCriadorId: NovoIdDeAto(instantePublicacao),
            atoCriadorHash: hashDocumento,
            atorUsuarioSub,
            instantePublicacao);

        Status = StatusProcesso.Publicado;

        AddDomainEvent(new ProcessoPublicadoEvent(
            Id,
            versao.AtoCriadorId,
            versao.Id,
            versao.HashConfiguracao,
            versao.AtoCriadorHash,
            // OccurredOn é o instante em que o fato ocorreu — o do SISTEMA, que é o da
            // vigência da versão. NÃO a data que o documento declara: ela é informada pelo
            // operador (ADR-0108) e pode ser retroativa (importação de acervo). Um consumidor
            // que resolvesse a configuração vigente no instante do ato (ADR-0075) usando a data
            // documental cairia antes da vigência e não acharia a versão que o próprio ato
            // criou — foi o defeito que a #803 corrigiu, e usar a data declarada aqui o
            // ressuscitaria por outra porta.
            versao.VigenteAPartirDe));

        return Result<VersaoConfiguracao>.Success(versao);
    }

    /// <summary>
    /// Retifica um processo já publicado (RN08, ADR-0101/0103): decide o ato que
    /// emenda o ato criador da <see cref="VersaoConfiguracao"/> corrente e sucede
    /// essa versão — a anterior permanece intocada (append-only). O status continua
    /// Publicado. Os bytes canônicos já vêm do <c>ISnapshotPublicacaoCanonicalizer</c>
    /// (Application) com o bloco de retificação incluído; esta raiz não os produz
    /// (ADR-0042).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retificar <b>não é um tipo de ato</b>: é uma relação entre atos (ADR-0103). O que
    /// muda em relação à abertura é só o par (ato emendado, motivo) — o tipo do ato
    /// continua vindo declarado pelo operador, e uma convocação retificada continua
    /// convocação.
    /// </para>
    /// <para>
    /// O alvo é o ato criador da versão corrente, e o servidor o <b>infere</b> — o cliente
    /// nunca o informa (ADR-0101). Ele é lido da CADEIA DE VERSÕES, não da data documental:
    /// é a versão que ordena a configuração, e a data pode regredir (relógio do host,
    /// importação de acervo) sem que isso mova o topo da cadeia.
    /// </para>
    /// </remarks>
    /// <param name="versaoAtual">Versão de configuração corrente do processo (maior <c>NumeroVersao</c>), carregada pelo handler — <see cref="VersaoConfiguracao"/> é agregado próprio, não coleção desta raiz.</param>
    /// <param name="motivo">Justificativa obrigatória do ato de retificação (ADR-0101).</param>
    public Result<VersaoConfiguracao> Retificar(
        DadosEdital dados,
        VersaoConfiguracao versaoAtual,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashDocumento,
        string atorUsuarioSub,
        string motivo,
        TimeProvider clock)
    {
        // A ordem é a de sempre, e ela importa: os contratos do método (argumentos não nulos)
        // e o estado do certame são conferidos ANTES da sessão editorial. Antepor a recusa por
        // `RetificacaoJaAberta` faria um `dados` nulo deixar de lançar, e um processo em estado
        // inválido deixar de acusar a transição — o atalho passaria a mentir sobre o motivo da
        // recusa, e mudaria de comportamento numa Feature que prometeu não tocá-lo.
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(configuracaoCongeladaCanonica);
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != StatusProcesso.Publicado)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {Status}."));
        }

        // O atalho atômico e a sessão editorial retificam o MESMO ato — o criador da versão
        // corrente. Deixá-los correr juntos publicaria a versão N+1 a partir da configuração
        // viva (que a sessão está no meio de editar), e o rascunho sobreviveria apontando
        // para uma base que deixou de ser o topo da cadeia: fechá-lo depois emendaria um ato
        // já emendado. A recusa é invariante do agregado, não `if` do handler (ADR-0110 D7).
        if (Rascunho is not null)
        {
            return Result<VersaoConfiguracao>.Failure(RetificacaoJaAberta());
        }

        return SucederVersao(
            dados, versaoAtual, configuracaoCongeladaCanonica, schemaVersion, algoritmoHash,
            hashDocumento, atorUsuarioSub, motivo, clock);
    }

    /// <summary>
    /// <b>Fecha</b> a sessão editorial: congela a versão N+1 <b>com a configuração editada</b>
    /// e encerra a sessão — na mesma transação (ADR-0110).
    /// </summary>
    /// <remarks>
    /// <para>
    /// É <b>aqui</b> que a Feature entrega o que ela existe para entregar. Abrir e descartar
    /// deixam o certame como estava; só o fechamento faz a configuração alterada virar
    /// documento — e é o que desbloqueia as dimensões que faltam ao Módulo Seleção.
    /// </para>
    /// <para>
    /// <b>O motivo vem do rascunho</b>, não do chamador: ele foi declarado na abertura,
    /// normalizado uma única vez, e é o mesmo que o bloco <c>retificacao</c> do envelope
    /// congela. Recebê-lo de novo aqui abriria a porta para os dois divergirem.
    /// </para>
    /// <para>
    /// <b>A sessão só morre depois de a versão estar decidida.</b> Se o congelamento for
    /// recusado — conformidade insuficiente, cadeia quebrada —, o rascunho <b>permanece
    /// aberto</b> e o administrador corrige e tenta de novo. Encerrá-la antes faria uma
    /// recusa de negócio destruir a sessão inteira.
    /// </para>
    /// </remarks>
    public Result<VersaoConfiguracao> FecharRetificacao(
        DadosEdital dados,
        VersaoConfiguracao versaoAtual,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashDocumento,
        string atorUsuarioSub,
        PrecondicaoIfMatch precondicao,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (PendenciaDaSessaoEditorial(precondicao) is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        Result<VersaoConfiguracao> versao = SucederVersao(
            dados, versaoAtual, configuracaoCongeladaCanonica, schemaVersion, algoritmoHash,
            hashDocumento, atorUsuarioSub, Rascunho!.Motivo, clock);
        if (versao.IsFailure)
        {
            return versao;
        }

        Rascunho = null;
        return versao;
    }

    /// <summary>
    /// <b>Descarta</b> a sessão editorial: o administrador abriu e desistiu (ADR-0110).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Este método não repõe nada — e é o ponto mais delicado da Feature.</b> Ele
    /// apenas <b>encerra</b> a sessão. A reposição da configuração congelada é da Application
    /// (o Domain não canonicaliza — ADR-0042) e tem de acontecer <b>antes</b>, via
    /// <c>IRestauradorDeConfiguracao</c>, que repõe <b>e prova</b> o round-trip byte a byte.
    /// </para>
    /// <para>
    /// <b>Encerrar a sessão sem repor é o pior desfecho possível</b>, e é preciso dizê-lo
    /// alto: enquanto a sessão existe, os seis <c>Definir*</c> escrevem <b>direto na
    /// configuração viva</b> — não há staging. Uma sessão encerrada sem reposição deixaria o
    /// certame de volta ao estado "publicado normal", servindo, em silêncio, uma configuração
    /// que <b>nunca foi publicada</b> e que diverge do documento que o publicou. Um fitness
    /// test prova que o único caller deste método restaura antes.
    /// </para>
    /// </remarks>
    public Result DescartarRetificacao(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (PendenciaDaSessaoEditorial(precondicao) is { } pendencia)
        {
            return Result.Failure(pendencia);
        }

        // O ESTADO INVÁLIDO É IRREPRESENTÁVEL, e não apenas desencorajado por um fitness test.
        //
        // Descartar sem repor devolveria o certame ao estado "publicado normal" servindo, em
        // silêncio, a configuração EDITADA — que nunca foi publicada. Um teste textual de
        // callers diz "ninguém faz isso hoje"; ele não diz "isso não pode acontecer". A
        // diferença aparece no dia em que um caminho novo esquecer a reposição, e o defeito é
        // dos que ninguém percebe: o status está certo, a versão congelada está intacta, e só
        // a configuração viva está mentindo.
        //
        // A prova é o próprio agregado: RestaurarConfiguracaoCongelada — que só o
        // IRestauradorDeConfiguracao chama, e que só repõe DEPOIS de provar o round-trip byte
        // a byte — carimba aqui a versão que repôs. O descarte exige esse carimbo, e exige que
        // ele seja o da versão que ESTA sessão tomou como base.
        if (_versaoRestaurada is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.DescarteSemRestauracao",
                "A sessão editorial não pode ser encerrada sem que a configuração congelada seja reposta — "
                + "encerrá-la agora deixaria o certame servindo a configuração editada como se ela tivesse sido publicada."));
        }

        if (_versaoRestaurada != Rascunho!.VersaoBaseId)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.DescarteComVersaoErrada",
                "A configuração reposta não é a da versão sobre a qual esta retificação foi aberta."));
        }

        Rascunho = null;
        _versaoRestaurada = null;
        return Result.Success();
    }

    /// <summary>
    /// A versão cuja configuração foi <b>reposta e provada</b> nesta unidade de trabalho —
    /// o carimbo que o descarte exige.
    /// </summary>
    /// <remarks>
    /// <b>Transiente de propósito.</b> Não é persistido nem mapeado: ele vive apenas dentro da
    /// transação, entre a reposição e o encerramento da sessão, e é justamente essa vida curta
    /// que o torna uma prova — um flag que sobrevivesse ao escopo autorizaria um descarte
    /// futuro com base numa restauração antiga.
    /// </remarks>
    private Guid? _versaoRestaurada;

    /// <summary>
    /// O que impede <b>encerrar</b> a sessão editorial (fechar ou descartar):
    /// <see langword="null"/> quando ela pode ser encerrada.
    /// </summary>
    /// <remarks>
    /// A ordem é a da ADR-0110 D9: a <b>inexistência</b> da sessão (409) precede a
    /// <b>precondição</b> (428/412) — responder 412 para um rascunho que não existe mandaria
    /// o cliente recarregar um ETag inexistente. A obrigatoriedade do <c>If-Match</c> aqui é
    /// <b>incondicional</b>: as duas rotas existem <i>para</i> a sessão.
    /// </remarks>
    private DomainError? PendenciaDaSessaoEditorial(PrecondicaoIfMatch precondicao)
    {
        if (Rascunho is null)
        {
            return RetificacaoNaoAberta();
        }

        return MutacaoBloqueada(precondicao);
    }

    /// <summary>
    /// O núcleo comum do <b>atalho atômico</b> (<see cref="Retificar"/>) e do
    /// <b>fechamento da sessão</b> (<see cref="FecharRetificacao"/>): sucede a cadeia de
    /// versões, decide o ato que emenda o anterior e drena o evento.
    /// </summary>
    /// <remarks>
    /// Os dois caminhos congelam <b>a mesma coisa</b> — a configuração viva, no estado em que
    /// ela está. O que os distingue é <b>de onde vem o motivo</b> e <b>o que acontece com a
    /// sessão</b>; tudo o mais é idêntico, e duplicá-lo faria as duas cadeias divergirem no
    /// dia em que uma delas mudasse.
    /// </remarks>
    private Result<VersaoConfiguracao> SucederVersao(
        DadosEdital dados,
        VersaoConfiguracao versaoAtual,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashDocumento,
        string atorUsuarioSub,
        string motivo,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(configuracaoCongeladaCanonica);
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != StatusProcesso.Publicado)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {Status}."));
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.MotivoRetificacaoObrigatorio",
                "O motivo da retificação é obrigatório."));
        }

        // A cadeia de versões não atravessa certames: uma versão corrente de
        // outro processo emendaria a configuração de um certame na de outro, e a
        // numeração — derivada dela — sairia do lugar.
        if (versaoAtual.ProcessoSeletivoId != Id)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "VersaoConfiguracao.VersaoAnteriorDeOutroProcesso",
                "A versão corrente informada pertence a outro Processo Seletivo."));
        }

        // O checklist vale para as DUAS transições que congelam. A retificação também
        // abre uma versão append-only e vinculante; congelar configuração incompleta
        // aqui produz um documento irreparável, exatamente como na publicação. Mesma
        // fonte, mesmo DomainError.
        if (PendenciaDeConformidade() is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        if (PendenciaDoCronograma() is { } pendenciaCronograma)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCronograma);
        }

        if (PendenciaDasExigenciasDocumentais() is { } pendenciaExigencias)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaExigencias);
        }

        if (PendenciaDeCoerenciaDaConsequenciaDeIndeferimento() is { } pendenciaCoerenciaConsequencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCoerenciaConsequencia);
        }

        if (PendenciaDaReferenciaTemporalFatos() is { } pendenciaReferenciaTemporal)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaReferenciaTemporal);
        }

        // Uma única leitura do relógio para o ato e para a versão que ele cria — ver a nota
        // em Publicar.
        //
        // E ela é ANCORADA na vigência da anterior antes de qualquer uso. Quando o relógio
        // regride (ajuste NTP em degrau), Suceder já ancorava a VIGÊNCIA; se o id do ato
        // nascesse do instante cru, as duas grandezas descreveriam instantes diferentes — e o
        // Guid v7 do ato novo, que carrega o timestamp nos 48 bits mais significativos,
        // ordenaria ANTES do ato que ele emenda. A ordenação cronológica por id (ADR-0032),
        // de que a paginação por keyset depende, inverteria a cadeia.
        DateTimeOffset agora = clock.GetUtcNow();
        DateTimeOffset instantePublicacao = agora < versaoAtual.VigenteAPartirDe
            ? versaoAtual.VigenteAPartirDe
            : agora;

        // ADR-0101/0104: a retificação emenda o ato que criou a VERSÃO CORRENTE — o
        // topo da cadeia de configuração —, e não o ato de maior data documental.
        // Ordenar o alvo por data seria frágil: a data é declarada pelo operador, e um
        // acervo migrado (ou um relógio que regrediu) pode dar ao ato mais NOVO uma data
        // mais ANTIGA. O topo por data e o topo por versão divergiriam, e uma cadeia
        // perfeitamente linear ficaria irretificável. É a versão que ordena a
        // configuração (ADR-0104) — inclusive para decidir o que se retifica.
        //
        // A linearidade da cadeia é garantida na MESMA transação por
        // ux_versoes_configuracao_ato_criador (um ato cria no máximo uma versão) e pelo
        // trigger de sucessão (ck_versoes_configuracao_cadeia). Publicações também a
        // barra, mas só no consumo da fila — backstop, não guard rail transacional.
        VersaoConfiguracao versao = VersaoConfiguracao.Suceder(
            versaoAtual,
            configuracaoCongeladaCanonica,
            schemaVersion,
            algoritmoHash,
            atoCriadorId: NovoIdDeAto(instantePublicacao),
            atoCriadorHash: hashDocumento,
            atoCriadorRetificaId: versaoAtual.AtoCriadorId,
            atorUsuarioSub,
            instantePublicacao);

        // Reaproveita ProcessoPublicadoEvent (não um evento distinto): o fato de
        // negócio drenado é "novo ato + nova versão da configuração", idêntico em
        // forma ao da abertura — o payload serve aos dois. Evita um segundo schema
        // Avro/tópico sem consumidor. O nome do membro EditalId é o histórico: ele é
        // contrato do envelope durável e do schema Avro, e o valor sempre foi o do
        // ato criador.
        AddDomainEvent(new ProcessoPublicadoEvent(
            Id,
            versao.AtoCriadorId,
            versao.Id,
            versao.HashConfiguracao,
            versao.AtoCriadorHash,
            versao.VigenteAPartirDe));

        return Result<VersaoConfiguracao>.Success(versao);
    }

    /// <summary>
    /// Repõe integralmente a configuração viva a partir do grafo reconstruído de uma
    /// <see cref="VersaoConfiguracao"/> congelada deste processo (ADR-0110 D2) — a
    /// operação que torna o descarte de uma sessão editorial <b>verificável</b>: o que
    /// volta é exatamente o que o documento publicado diz, não uma aproximação dele.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Não é um <c>Definir*</c>, e não passa pela trava de mutação pós-publicação.</b>
    /// Os <c>Definir*</c> são edição — mudam o que a configuração diz. Esta reposição
    /// é o contrário: devolve a configuração ao que a versão congelada já dizia. Por
    /// isso ela exige o processo <b>publicado</b> (só aí existe versão a restaurar) e é
    /// ancorada na própria evidência forense — <b>a versão é a credencial</b>, no mesmo
    /// desenho de <see cref="Retificar"/>, que também recebe a versão corrente e confia
    /// ao handler a escolha dela (<see cref="VersaoConfiguracao"/> é agregado próprio,
    /// ADR-0104; a raiz não conhece a cadeia).
    /// </para>
    /// <para>
    /// <b>Reposição integral, validada no estado final.</b> Não é uma sequência de
    /// <c>Definir*</c>: eles validam referências cruzadas contra o estado <i>corrente</i>
    /// (o desempate contra as etapas de agora, a classificação contra as etapas de
    /// agora) e recusariam um grafo meio-construído — a ordem em que as dimensões
    /// entrassem decidiria se a reposição passa. Aqui o grafo é validado <b>inteiro,
    /// como ele ficará</b>, e só então aplicado: uma restauração que falha não deixa o
    /// agregado meio-reposto.
    /// </para>
    /// <para>
    /// <b>Identidade (D2).</b> As etapas são reconciliadas <b>por <c>Id</c></b>: uma
    /// etapa que ainda existe na instância <i>tracked</i> é atualizada nela mesma —
    /// substituí-la por uma instância nova com o mesmo <c>Id</c> colidiria com o
    /// identity map do EF, e o <c>CreatedAt</c> original se perderia. As demais filhas
    /// são recriadas: os ids técnicos e o <c>CreatedAt</c> delas <b>não sobrevivem</b> —
    /// perda de informação <b>declarada</b> (ADR-0110 D2), não silenciosa. A auditoria
    /// com peso jurídico vive na <see cref="VersaoConfiguracao"/>, que é append-only e
    /// não é tocada aqui.
    /// </para>
    /// </remarks>
    /// <param name="versao">Versão congelada de onde o grafo foi reconstruído — deste processo, e a que o handler elegeu como vigente.</param>
    /// <param name="grafo">As seis dimensões reconstruídas pelo codec do envelope (ADR-0110 D1).</param>
    public Result RestaurarConfiguracaoCongelada(VersaoConfiguracao versao, GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(grafo);

        if (Status != StatusProcesso.Publicado)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.RestauracaoForaDePublicado",
                $"Só é possível restaurar a configuração congelada de um processo publicado — status atual: {Status}."));
        }

        // A cadeia de versões não atravessa certames: restaurar aqui a configuração
        // congelada de OUTRO processo sobrescreveria este com uma configuração que
        // nunca foi dele — e o envelope da próxima publicação congelaria a troca.
        if (versao.ProcessoSeletivoId != Id)
        {
            return Result.Failure(new DomainError(
                "VersaoConfiguracao.VersaoDeOutroProcesso",
                "A versão de configuração informada pertence a outro Processo Seletivo."));
        }

        if (ValidarGrafo(grafo) is { } erro)
        {
            return Result.Failure(erro);
        }

        AplicarGrafo(grafo);

        // Carimba a reposição. É o que torna o descarte sem restauração IRREPRESENTÁVEL: sem
        // este registro, DescartarRetificacao recusa. A sombra de verificação carimba a si
        // mesma e morre com o escopo — só a raiz viva leva o carimbo adiante.
        _versaoRestaurada = versao.Id;

        return Result.Success();
    }

    /// <summary>
    /// Uma <b>sombra</b> deste processo — mesma identidade, mesmo tipo, mesmo status, mas
    /// <b>sem configuração</b> e <b>fora do change tracker</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe por uma razão só: a fidelidade de uma reidratação só é demonstrável
    /// <b>recanonicalizando o agregado já reposto</b> — e fazer isso na raiz viva
    /// significaria mutá-la <b>antes</b> de saber se a reposição é boa. Uma prova que
    /// falhasse deixaria o agregado <i>tracked</i> empobrecido, e bastaria um
    /// <c>SaveChanges</c> adiante no mesmo escopo para gravar o estrago — a atomicidade
    /// dependeria da disciplina de quem chama, não do código.
    /// </para>
    /// <para>
    /// Com a sombra, a ordem se inverte: <b>prova primeiro, aplica depois</b>. A raiz viva
    /// só é tocada quando os bytes já bateram. O <c>Id</c> é o mesmo de propósito — é ele
    /// que as filhas recebem em <c>VincularProcesso</c>, e é ele que a versão congelada
    /// referencia.
    /// </para>
    /// </remarks>
    public ProcessoSeletivo SombraParaVerificacao() => new()
    {
        Id = Id,
        Nome = Nome,
        Tipo = Tipo,
        Status = Status,
        OrigemCandidatos = OrigemCandidatos,
    };

    /// <summary>
    /// Valida o grafo <b>como ele ficará</b> — as referências cruzadas são resolvidas
    /// contra as etapas do PRÓPRIO grafo, não contra as do agregado, que estão prestes
    /// a ser substituídas. Nenhuma escrita acontece antes desta função devolver
    /// <see langword="null"/>.
    /// </summary>
    private static bool HaAcaoQuandoIndeferidoDivergenteEntreOfertas(IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicaoVagas) =>
        distribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .GroupBy(static m => m.Codigo, StringComparer.Ordinal)
            .Any(static grupo => grupo.Select(static m => m.AcaoQuandoIndeferido).Distinct().Count() > 1);

    private DomainError? ValidarGrafo(GrafoConfiguracao grafo)
    {
        // Story #851 §3.5: lista de etapas vazia é estado válido (processo sem prova,
        // ex. SiSU) — a antiga recusa incondicional foi removida também aqui, espelhando
        // a mudança em DefinirEtapas.
        //
        // O Id vem congelado do envelope (D2) — e nem EtapaProcesso nem o agregado o
        // validavam: a unicidade era garantida só pelo handler de PUT /etapas. Um
        // envelope com dois ids iguais produziria duas etapas que o etapa_ref não
        // consegue distinguir, e o INSERT colidiria na chave primária.
        List<Guid> idsEtapas = [.. grafo.Etapas.Select(e => e.Id)];
        if (idsEtapas.Any(id => id == Guid.Empty))
        {
            return new DomainError(
                "ProcessoSeletivo.IdEtapaAusente",
                "Toda etapa restaurada deve declarar o Id congelado no envelope.");
        }

        if (idsEtapas.Distinct().Count() != idsEtapas.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.IdEtapaDuplicado",
                "O mesmo Id de etapa não pode aparecer mais de uma vez na configuração restaurada.");
        }

        List<int> ordensEtapas = [.. grafo.Etapas.Where(e => e.Ordem.HasValue).Select(e => e.Ordem!.Value)];
        if (ordensEtapas.Distinct().Count() != ordensEtapas.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemEtapaDuplicada",
                "Cada etapa deve ter uma ordem única dentro do processo.");
        }

        if (grafo.Etapas.Count > 0 && !grafo.Etapas.Any(e => e.ComponeNota))
        {
            return new DomainError(
                "ProcessoSeletivo.NenhumaEtapaComponeNota",
                "Ao menos uma etapa deve ter caráter classificatória ou ambas, com peso, para compor a nota final.");
        }

        if (grafo.DistribuicaoVagas.Count == 0)
        {
            return new DomainError(
                "ProcessoSeletivo.DistribuicaoVagasVazia",
                "O processo deve ter ao menos uma distribuição de vagas configurada.");
        }

        List<Guid> ofertas = [.. grafo.DistribuicaoVagas.Select(d => d.OfertaCursoOrigemId)];
        if (ofertas.Distinct().Count() != ofertas.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OfertaCursoDuplicada",
                "Cada oferta de curso só pode ter uma distribuição de vagas no processo.");
        }

        // issue #848/ADR-0115 §3.7 — mesma checagem de DefinirDistribuicaoVagas: a
        // restauração aplica o grafo diretamente via AplicarGrafo, sem passar por
        // aquele método, então precisa repetir a validação de consistência.
        if (HaAcaoQuandoIndeferidoDivergenteEntreOfertas(grafo.DistribuicaoVagas))
        {
            return new DomainError(
                "ProcessoSeletivo.AcaoQuandoIndeferidoDivergente",
                "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo.");
        }

        List<int> ordensDesempate = [.. grafo.CriteriosDesempate.Select(c => c.Ordem)];
        if (ordensDesempate.Distinct().Count() != ordensDesempate.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemDesempateDuplicada",
                "Cada critério de desempate deve ter uma ordem única dentro do processo.");
        }

        // INV-B6 — contra as etapas DO GRAFO. Se o codec regenerasse o etapa.Id em vez
        // de preservá-lo, é aqui que a restauração pararia: o etapaRef congelado deixa
        // de resolver.
        IEnumerable<CriterioDesempate> porEtapa = grafo.CriteriosDesempate
            .Where(static c => c.Args is ArgsDesempateMaiorNotaEtapa);
        foreach (CriterioDesempate criterio in porEtapa)
        {
            ArgsDesempateMaiorNotaEtapa args = (ArgsDesempateMaiorNotaEtapa)criterio.Args;
            if (!idsEtapas.Contains(args.EtapaRef))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaRefDesempateInexistente",
                    $"O critério de desempate na ordem {criterio.Ordem} referencia a etapa {args.EtapaRef}, que não existe na configuração restaurada (INV-B6).");
            }
        }

        bool baseadoEmEnem = Tipo is TipoProcesso.SiSU or TipoProcesso.PSVR;
        foreach (RegraEliminacao regra in grafo.Classificacao.RegrasEliminacao)
        {
            // INV-B4 — mesma proteção do INV-B6, para a eliminação por nota mínima.
            if (regra.Args is ArgsElimNotaMinimaEtapa notaMinima && !idsEtapas.Contains(notaMinima.EtapaRef))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
                    $"A regra de eliminação referencia a etapa {notaMinima.EtapaRef}, que não existe na configuração restaurada (INV-B4).");
            }

            if (regra.Args is ArgsElimCorteRedacao or ArgsElimZeroEmArea && !baseadoEmEnem)
            {
                return new DomainError(
                    "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
                    $"A regra {regra.Regra.Codigo} só se aplica a processo baseado em ENEM (SiSU/PSVR).");
            }
        }

        // Story #851 — cronograma restaurado: checagens estruturais equivalentes às de
        // DefinirCronogramaFases, sobre o GRAFO (não sobre o agregado corrente). O grafo
        // de precedências e a resolução de regra/ato âncora NÃO são reconferidos aqui —
        // são I/O, e RN08 proíbe reinterpretar um passado legitimamente publicado contra
        // o catálogo de hoje (mesma doutrina de LeitorEnvelope.Regra).
        if (grafo.CronogramaFases.Count == 0)
        {
            return new DomainError(
                "ProcessoSeletivo.CronogramaFasesVazio",
                "O processo deve ter ao menos uma fase no cronograma.");
        }

        List<int> ordensFases = [.. grafo.CronogramaFases.Select(f => f.Ordem)];
        if (ordensFases.Distinct().Count() != ordensFases.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemFaseDuplicada",
                "Cada fase deve ter uma ordem única dentro do cronograma.");
        }

        List<Guid> origensFases = [.. grafo.CronogramaFases.Select(f => f.FaseCanonicaOrigemId)];
        if (origensFases.Distinct().Count() != origensFases.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.FaseCanonicaDuplicada",
                "A mesma fase canônica não pode aparecer duas vezes no cronograma.");
        }

        bool existeFaseDeAvaliacaoNoGrafo = grafo.CronogramaFases.Any(static f => f.AgrupaEtapas);
        if (existeFaseDeAvaliacaoNoGrafo && grafo.Etapas.Count == 0)
        {
            return new DomainError(
                "ProcessoSeletivo.AvaliacaoSemEtapa",
                "Há uma fase que agrupa etapas no cronograma restaurado, mas nenhuma etapa pontuada.");
        }

        if (grafo.Etapas.Count > 0 && !existeFaseDeAvaliacaoNoGrafo)
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
                "Há etapa pontuada no grafo restaurado, mas nenhuma fase agrupa etapas.");
        }

        return null;
    }

    /// <summary>
    /// Aplica o grafo já validado. Chamado <b>só</b> depois de <see cref="ValidarGrafo"/>
    /// devolver <see langword="null"/> — a partir daqui não há caminho de falha, e é o
    /// que garante que uma restauração recusada não altere nada (ADR-0110 D2).
    /// </summary>
    private void AplicarGrafo(GrafoConfiguracao grafo)
    {
        // Reconciliação por Id (a armadilha do EF): a instância tracked é REUSADA e
        // atualizada, nunca substituída por uma instância nova com o mesmo Id — isso
        // colidiria com o identity map. Mesmo padrão de DefinirEtapasCommandHandler, e
        // é também o que preserva o CreatedAt original das etapas sobreviventes (D2).
        Dictionary<Guid, EtapaProcesso> tracked = _etapas.ToDictionary(e => e.Id);
        List<EtapaProcesso> etapas = [];
        foreach (EtapaProcesso congelada in grafo.Etapas)
        {
            if (tracked.TryGetValue(congelada.Id, out EtapaProcesso? viva))
            {
                viva.AtualizarDados(
                    congelada.Nome,
                    congelada.Carater,
                    congelada.Peso,
                    congelada.NotaMinima,
                    congelada.Ordem);
                etapas.Add(viva);
            }
            else
            {
                etapas.Add(congelada);
            }
        }

        _etapas.Clear();
        foreach (EtapaProcesso etapa in etapas)
        {
            etapa.VincularProcesso(Id);
            _etapas.Add(etapa);
        }

        grafo.OfertaAtendimento.VincularProcesso(Id);
        OfertaAtendimento = grafo.OfertaAtendimento;

        _distribuicaoVagas.Clear();
        foreach (ConfiguracaoDistribuicaoVagas configuracao in grafo.DistribuicaoVagas)
        {
            configuracao.VincularProcesso(Id);
            _distribuicaoVagas.Add(configuracao);
        }

        grafo.BonusRegional?.VincularProcesso(Id);
        BonusRegional = grafo.BonusRegional;

        _criteriosDesempate.Clear();
        foreach (CriterioDesempate criterio in grafo.CriteriosDesempate)
        {
            criterio.VincularProcesso(Id);
            _criteriosDesempate.Add(criterio);
        }

        grafo.Classificacao.VincularProcesso(Id);
        Classificacao = grafo.Classificacao;

        // Cronograma de fases (Story #851): nenhuma referência externa aponta para
        // FaseCronograma.Id (diferente das etapas) — o Id não é congelado no envelope
        // (§3.7) e nunca sobrevive à reidratação. Por isso a reconciliação é por
        // ORDEM, não por Id: reusa a instância TRACKED cuja Ordem bate com a da fase
        // congelada, atualizando-a no lugar (mesmo cuidado do EF que as etapas já
        // tomam — ver a nota em FaseCronograma.AtualizarSnapshot). Sem isso, o caso
        // comum de restauração (mesmas ordens, dados diferentes) faria DELETE+INSERT
        // do mesmo valor de Ordem na mesma transação, colidindo em
        // ux_fases_cronograma_processo_ordem — o EF não infere essa ordem entre
        // entidades sem relação de FK.
        Dictionary<int, FaseCronograma> fasesTracked = _cronogramaFases.ToDictionary(f => f.Ordem);
        List<FaseCronograma> fases = [];
        foreach (FaseCronograma congelada in grafo.CronogramaFases)
        {
            if (fasesTracked.TryGetValue(congelada.Ordem, out FaseCronograma? viva))
            {
                viva.AtualizarSnapshot(
                    congelada.FaseCanonicaOrigemId,
                    congelada.Ordem,
                    congelada.Codigo,
                    congelada.DonoInstitucional,
                    congelada.OrigemData,
                    congelada.AgrupaEtapas,
                    congelada.PermiteComplementacao,
                    congelada.ProduzResultado,
                    congelada.ResultadoDefinitivo,
                    congelada.ColetaInscricao,
                    congelada.Inicio,
                    congelada.Fim,
                    congelada.AtoProduzidoCodigo,
                    congelada.AtoProduzidoEfeitoIrreversivel,
                    [.. congelada.BancasRequeridas],
                    congelada.RegraRecurso);
                fases.Add(viva);
            }
            else
            {
                fases.Add(congelada);
            }
        }

        _cronogramaFases.Clear();
        foreach (FaseCronograma fase in fases)
        {
            fase.VincularProcesso(Id);
            _cronogramaFases.Add(fase);
        }

        // Documentos exigidos (Story #554, PR #903): o bloco `documentosExigidos.exigencias`
        // do envelope agora é real (CA-09) — reconciliação por `exigenciaId` (o
        // DocumentoExigido.Id preservado por Reidratar, o segundo caso de identidade
        // congelada depois de EtapaProcesso.Id, ADR-0110 D2). Mesmo padrão de reuso da
        // instância TRACKED das demais coleções acima.
        Dictionary<Guid, DocumentoExigido> documentosExigidosTracked = _documentosExigidos.ToDictionary(d => d.Id);
        List<DocumentoExigido> documentosExigidos = [];
        foreach (DocumentoExigido congelada in grafo.DocumentosExigidos)
        {
            documentosExigidos.Add(documentosExigidosTracked.TryGetValue(congelada.Id, out DocumentoExigido? viva) ? viva : congelada);
        }

        _documentosExigidos.Clear();
        foreach (DocumentoExigido documento in documentosExigidos)
        {
            documento.VincularProcesso(Id);
            _documentosExigidos.Add(documento);
        }

        // Referência temporal de fatos (Story #554, PR #903, B-03): o envelope congela a
        // POLÍTICA crua (Tipo/Data/FaseId) ao lado da data já resolvida — mesmo padrão de
        // "insumo + output derivado" de distribuicao/vagas. Repor a política (não só a
        // data) é o que torna o round-trip reidratar→recanonicalizar não-tautológico:
        // ResolverDataReferenciaFatos() recalcula o mesmo output a partir do mesmo insumo
        // restaurado. Cada VersaoConfiguracao congela sua PRÓPRIA política — uma
        // retificação que muda a política antes de publicar não afeta o que já foi
        // congelado (B-03).
        ReferenciaTemporalFatos = grafo.ReferenciaTemporalFatos;
    }

    /// <summary>
    /// Decide o identificador do ato que cria uma versão. Guid v7 ancorado no
    /// <b>mesmo instante</b> já lido para a versão (ADR-0068) — nunca num relógio
    /// próprio: o id do ato e a vigência da versão que ele cria descrevem o mesmo
    /// fato, e derivar cada um de uma leitura diferente os faria discordar.
    /// </summary>
    /// <remarks>
    /// O id nasce aqui, e não em <c>Publicacoes</c>, porque a versão precisa
    /// referenciá-lo dentro desta transação — antes de o ato existir fisicamente
    /// (ADR-0108). É também o que torna a reentrega da fila (at-least-once)
    /// idempotente: o segundo processamento tenta gravar o MESMO id, e a chave
    /// primária o recusa.
    /// </remarks>
    private static Guid NovoIdDeAto(DateTimeOffset instante) => Guid.CreateVersion7(instante);

    /// <summary>
    /// Abre a <b>sessão editorial</b> sobre a configuração de um certame publicado
    /// (ADR-0110 D3) — o que destrava os seis <c>Definir*</c> sem que o certame mude de
    /// estado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>O <see cref="Status"/> não muda</b>, e é a decisão central da ADR: um certame
    /// juridicamente publicado que exibisse um status sugerindo o contrário mentiria para
    /// o candidato — e um rascunho abandonado o deixaria assim indefinidamente. O que
    /// muda é a <b>existência</b> do <see cref="Rascunho"/>.
    /// </para>
    /// <para>
    /// <b>Nada é congelado aqui.</b> Abrir não emite ato, não abre
    /// <see cref="VersaoConfiguracao"/> e não drena evento nenhum — a versão nova nasce só
    /// no fechamento. Enquanto a sessão está aberta, o que vale para o mundo continua
    /// sendo a versão congelada vigente.
    /// </para>
    /// </remarks>
    /// <param name="motivo">Justificativa do ato de retificação — normalizada e validada em <see cref="RascunhoRetificacao"/>.</param>
    /// <param name="versaoBase">A versão corrente do processo, eleita pelo handler (<see cref="VersaoConfiguracao"/> é agregado próprio — ADR-0104).</param>
    /// <param name="abertoPorSub">Sub do usuário autenticado (via <c>IUserContext</c>, nunca input do command).</param>
    /// <param name="abertoEm">Instante lido do relógio injetado (ADR-0068).</param>
    public Result<RascunhoRetificacao> AbrirRetificacao(
        string motivo,
        VersaoConfiguracao versaoBase,
        string abertoPorSub,
        DateTimeOffset abertoEm)
    {
        ArgumentNullException.ThrowIfNull(versaoBase);

        if (Status != StatusProcesso.Publicado)
        {
            return Result<RascunhoRetificacao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {Status}."));
        }

        if (versaoBase.ProcessoSeletivoId != Id)
        {
            return Result<RascunhoRetificacao>.Failure(new DomainError(
                "VersaoConfiguracao.VersaoDeOutroProcesso",
                "A versão de configuração informada pertence a outro Processo Seletivo."));
        }

        if (Rascunho is not null)
        {
            return Result<RascunhoRetificacao>.Failure(RetificacaoJaAberta());
        }

        Result<RascunhoRetificacao> rascunho = RascunhoRetificacao.Criar(
            Id, motivo, versaoBase, abertoPorSub, abertoEm);
        if (rascunho.IsFailure)
        {
            return rascunho;
        }

        Rascunho = rascunho.Value!;
        return rascunho;
    }

    /// <summary>
    /// Altera o motivo da sessão editorial em curso. Como toda mutação sob sessão, exige a
    /// precondição e <b>incrementa a revisão</b> (D5).
    /// </summary>
    public Result AlterarMotivoRetificacao(string motivo, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        // A inexistência da sessão vem ANTES da precondição (D9): responder 412 para um
        // rascunho que não existe mandaria o cliente recarregar um ETag inexistente.
        if (Rascunho is null)
        {
            return Result.Failure(RetificacaoNaoAberta());
        }

        // E daqui em diante é o MESMO guard dos seis Definir* — não uma checagem paralela.
        // Alterar o motivo é mutação como as outras, e precisa da allowlist inteira: um
        // processo que saísse de Publicado com a sessão ainda aberta continuaria aceitando
        // esta rota se ela só conferisse a precondição, e a edição escaparia por uma porta
        // que os Definir* já tinham fechado.
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        Result alterado = Rascunho.AlterarMotivo(motivo);
        if (alterado.IsFailure)
        {
            return alterado;
        }

        Rascunho.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// O <c>ETag</c> forte da sessão editorial em curso, ou <see langword="null"/> quando
    /// não há sessão — é o que o cliente devolve no <c>If-Match</c> da próxima mutação.
    /// </summary>
    public string? ETagDaSessaoEditorial => Rascunho?.ETag;

    /// <summary>
    /// <b>Allowlist</b> de mutação da configuração (ADR-0110 D4). O que era uma
    /// <b>denylist de um elemento</b> — "bloqueia se, e só se, está publicado" — e por
    /// isso <b>falhava aberta</b>: <see cref="StatusProcesso.Nenhum"/>,
    /// <see cref="StatusProcesso.Encerrado"/> e <see cref="StatusProcesso.Cancelado"/>
    /// eram silenciosamente mutáveis, e todo estado futuro nasceria mutável por omissão.
    /// </summary>
    /// <remarks>
    /// <c>MutacaoPermitida() ⟺ Status == Rascunho || (Status == Publicado &amp;&amp; rascunho aberto)</c>
    /// </remarks>
    private bool MutacaoPermitida() =>
        Status == StatusProcesso.Rascunho
        || (Status == StatusProcesso.Publicado && Rascunho is not null);

    /// <summary>
    /// Guard único de todo <c>Definir*</c>: a allowlist acima <b>mais</b> a precondição de
    /// concorrência quando há sessão editorial aberta. <see langword="null"/> quando a
    /// mutação pode prosseguir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A obrigatoriedade do <c>If-Match</c> é condicional ao estado</b>, e é por isso
    /// que ela vive aqui e não no filtro de transporte: os mesmos seis <c>Definir*</c>
    /// servem um processo em <see cref="StatusProcesso.Rascunho"/> (pré-publicação — sem
    /// sessão, e portanto sem ETag a fornecer) e a edição <b>durante</b> uma retificação
    /// (com sessão, e com precondição obrigatória). Só quem carregou o agregado sabe em
    /// qual dos dois está.
    /// </para>
    /// <para>
    /// Os handlers chamam este guard <b>logo após</b> resolverem o 404 — antes de validar
    /// payload —, porque a precondição precede a regra de negócio na ordem de avaliação
    /// (D9). Mas ele continua sendo chamado <b>aqui dentro</b> por cada <c>Definir*</c>:
    /// a antecipação dá a ordem correta, e o guard no domínio garante que ela não seja
    /// contornável por um handler futuro que esqueça de antecipá-la.
    /// </para>
    /// </remarks>
    public DomainError? MutacaoBloqueada(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (Status == StatusProcesso.Publicado && Rascunho is null)
        {
            return new DomainError(
                "ProcessoSeletivo.MutacaoPosPublicacaoBloqueada",
                "Processo publicado não aceita mutação direta da configuração — utilize a retificação.");
        }

        if (!MutacaoPermitida())
        {
            return new DomainError(
                "ProcessoSeletivo.MutacaoForaDeEstadoEditavel",
                $"Um processo em {Status} não aceita mutação da configuração.");
        }

        // Processo em Rascunho: não há sessão editorial, e portanto não há ETag que o
        // cliente pudesse fornecer. Exigir a precondição aqui quebraria toda a edição
        // pré-publicação.
        return Rascunho?.ConferirPrecondicao(precondicao);
    }

    internal static DomainError RetificacaoJaAberta() => new(
        "RascunhoRetificacao.JaAberta",
        "Já existe uma retificação em curso neste processo — feche-a ou descarte-a antes de abrir outra.");

    private static DomainError RetificacaoNaoAberta() => new(
        "RascunhoRetificacao.NaoAberta",
        "Não há retificação em curso neste processo.");
}
