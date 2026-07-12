namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Events;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
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

    private readonly List<EtapaProcesso> _etapas = [];
    public IReadOnlyCollection<EtapaProcesso> Etapas => _etapas.AsReadOnly();

    public OfertaAtendimentoEspecializado? OfertaAtendimento { get; private set; }

    private readonly List<ConfiguracaoDistribuicaoVagas> _distribuicaoVagas = [];
    public IReadOnlyCollection<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas => _distribuicaoVagas.AsReadOnly();

    /// <summary>Bônus regional (RN05) — ausência = sem bônus (toggle por presença, INV-B5).</summary>
    public ConfiguracaoBonusRegional? BonusRegional { get; private set; }

    private readonly List<CriterioDesempate> _criteriosDesempate = [];
    public IReadOnlyCollection<CriterioDesempate> CriteriosDesempate => _criteriosDesempate.AsReadOnly();

    /// <summary>Configuração de classificação (15º bloco canônico, Story #775) — compõe por referência a fórmula, precisão, eliminação e ordem de alocação.</summary>
    public ConfiguracaoClassificacao? Classificacao { get; private set; }

    private ProcessoSeletivo() { }

    public static ProcessoSeletivo Criar(string nome, TipoProcesso tipo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (tipo == TipoProcesso.Nenhum)
        {
            throw new ArgumentException("Tipo do processo é obrigatório.", nameof(tipo));
        }

        return new ProcessoSeletivo
        {
            Nome = nome.Trim(),
            Tipo = tipo,
            Status = StatusProcesso.Rascunho,
        };
    }

    /// <summary>
    /// Substitui integralmente as etapas pontuadas do processo. A ordem, o
    /// caráter e o peso definem o divisor da média
    /// (<see cref="CalcularDivisorMedia"/>).
    /// </summary>
    public Result DefinirEtapas(IReadOnlyList<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        if (MutacaoBloqueadaPosPublicacao() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (etapas.Count == 0)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.EtapasVazias",
                "O processo deve ter ao menos uma etapa pontuada."));
        }

        List<int> ordensInformadas = [.. etapas.Where(e => e.Ordem.HasValue).Select(e => e.Ordem!.Value)];
        if (ordensInformadas.Distinct().Count() != ordensInformadas.Count)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.OrdemEtapaDuplicada",
                "Cada etapa deve ter uma ordem única dentro do processo."));
        }

        // Sem ao menos uma etapa que componha a nota, CalcularDivisorMedia()
        // retorna 0 — um processo só com etapas eliminatórias (ou
        // classificatórias sem peso) prepararia divisão por zero na fórmula
        // da nota final (NOTA FINAL = Soma(Etapa×peso) / divisor).
        if (!etapas.Any(e => e.ComponeNota))
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

        return Result.Success();
    }

    /// <summary>
    /// Define (ou substitui) a oferta de atendimento especializado do processo.
    /// A invariante ADR-0067 (tipo de deficiência só sob condição PcD) já foi
    /// garantida na montagem da oferta
    /// (<see cref="OfertaAtendimentoEspecializado.Criar"/>).
    /// </summary>
    public Result DefinirOfertaAtendimento(OfertaAtendimentoEspecializado oferta)
    {
        ArgumentNullException.ThrowIfNull(oferta);

        if (MutacaoBloqueadaPosPublicacao() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        oferta.VincularProcesso(Id);
        OfertaAtendimento = oferta;
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
    public Result DefinirDistribuicaoVagas(IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas)
    {
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);

        if (MutacaoBloqueadaPosPublicacao() is { } bloqueio)
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

        _distribuicaoVagas.Clear();
        foreach (ConfiguracaoDistribuicaoVagas configuracao in distribuicaoVagas)
        {
            configuracao.VincularProcesso(Id);
            _distribuicaoVagas.Add(configuracao);
        }

        return Result.Success();
    }

    /// <summary>
    /// Define (ou remove) o bônus regional do processo (RN05). Passar
    /// <see langword="null"/> remove o bônus — a ausência da entidade já é o
    /// toggle "sem bônus" (INV-B5); não existe um "BONUS-NENHUM".
    /// </summary>
    public Result DefinirBonusRegional(ConfiguracaoBonusRegional? bonus)
    {
        if (MutacaoBloqueadaPosPublicacao() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (bonus is null)
        {
            BonusRegional = null;
            return Result.Success();
        }

        bonus.VincularProcesso(Id);
        BonusRegional = bonus;
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
    public Result DefinirCriteriosDesempate(IReadOnlyList<CriterioDesempate> criterios)
    {
        ArgumentNullException.ThrowIfNull(criterios);

        if (MutacaoBloqueadaPosPublicacao() is { } bloqueio)
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
    public Result DefinirClassificacao(ConfiguracaoClassificacao classificacao)
    {
        ArgumentNullException.ThrowIfNull(classificacao);

        if (MutacaoBloqueadaPosPublicacao() is { } bloqueio)
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
    /// estruturalmente OBRIGATÓRIAS do agregado: Etapas (1..*), Oferta de
    /// atendimento especializado (1), Distribuição de vagas (1..*) e
    /// Classificação (1). Bônus regional (0..1) e critérios de desempate
    /// (0..*) são deliberadamente opcionais e NÃO entram — a ausência é um
    /// estado válido (RN05: ausência de bônus = sem bônus), não uma
    /// pendência. Única fonte de verdade do checklist: tanto
    /// <c>ObterConformidadeProcessoSeletivoQueryHandler</c> quanto
    /// <see cref="Publicar"/> chamam este método, nunca duplicam a lista.
    /// </summary>
    public IReadOnlyList<ItemConformidade> AvaliarConformidade() =>
    [
        new ItemConformidade("Etapas", _etapas.Count > 0),
        new ItemConformidade("Atendimento especializado", OfertaAtendimento is not null),
        new ItemConformidade("Distribuição de vagas", _distribuicaoVagas.Count > 0),
        new ItemConformidade("Classificação", Classificacao is not null),
    ];

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

        IReadOnlyList<ItemConformidade> pendencias = [.. AvaliarConformidade().Where(item => !item.Ok)];
        if (pendencias.Count > 0)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.ConformidadeInsuficiente",
                $"Processo não conforme para publicação — pendente: {string.Join(", ", pendencias.Select(p => p.Item))}."));
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
    /// Trava de mutação pós-publicação (CA-04): todo <c>Definir*</c> recusa
    /// quando o processo já foi publicado — mudança em conteúdo congelável só
    /// via retificação. Reaproveitada por todos os métodos <c>Definir*</c>;
    /// <see langword="null"/> quando a mutação é permitida.
    /// </summary>
    private DomainError? MutacaoBloqueadaPosPublicacao()
    {
        if (Status != StatusProcesso.Publicado)
        {
            return null;
        }

        return new DomainError(
            "ProcessoSeletivo.MutacaoPosPublicacaoBloqueada",
            "Processo publicado não aceita mutação direta da configuração — utilize a retificação.");
    }
}
