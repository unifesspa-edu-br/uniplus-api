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
/// O <c>Edital</c> não é criado aqui: ele é o documento emitido pelo ato de
/// publicação (F5), que congela esta configuração num snapshot imutável
/// (RN08). Enquanto o processo está em rascunho, a configuração é livremente
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

    /// <summary>
    /// Editais emitidos pelo ato de publicação/retificação (Story #759, T4
    /// #785) — só cresce dentro de <see cref="Publicar"/> (e, na T5, de
    /// <c>Retificar</c>); nunca exposta como <c>Definir*</c> mutável.
    /// </summary>
    private readonly List<Edital> _editais = [];
    public IReadOnlyCollection<Edital> Editais => _editais.AsReadOnly();

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
    /// conformidade estrutural, emite o <see cref="Edital"/> de abertura,
    /// congela o <see cref="SnapshotPublicacao"/> a partir dos bytes
    /// canônicos já produzidos pelo <c>ISnapshotPublicacaoCanonicalizer</c>
    /// (Application — Domain não pode chamá-lo, ver ADR-0042) e transita o
    /// status — tudo dentro deste método, atomicamente em memória; o handler
    /// só persiste o resultado numa única transação.
    /// </summary>
    /// <param name="dados">Número, período de inscrição e referência ao documento confirmado.</param>
    /// <param name="configuracaoCongeladaCanonica">Bytes canônicos (ADR-0100) já produzidos pelo canonicalizador da Application.</param>
    /// <param name="schemaVersion">Versão do conjunto de blocos do snapshot (ADR-0100 item 8).</param>
    /// <param name="algoritmoHash">Identificador do algoritmo de hash (ex.: <c>canonical-json/sha256@v1</c>).</param>
    /// <param name="hashEdital">Hash SHA-256 do documento do Edital (T3, #784).</param>
    /// <param name="atorUsuarioSub">Sub do usuário autenticado responsável pela publicação (via <c>IUserContext</c>, nunca input do command).</param>
    /// <param name="clock">Relógio injetado (ADR-0068) — nunca lido implicitamente.</param>
    public Result<PublicacaoResultado> Publicar(
        DadosEdital dados,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashEdital,
        string atorUsuarioSub,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(configuracaoCongeladaCanonica);
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != StatusProcesso.Rascunho)
        {
            return Result<PublicacaoResultado>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível publicar um processo em rascunho — status atual: {Status}."));
        }

        IReadOnlyList<ItemConformidade> pendencias = [.. AvaliarConformidade().Where(item => !item.Ok)];
        if (pendencias.Count > 0)
        {
            return Result<PublicacaoResultado>.Failure(new DomainError(
                "ProcessoSeletivo.ConformidadeInsuficiente",
                $"Processo não conforme para publicação — pendente: {string.Join(", ", pendencias.Select(p => p.Item))}."));
        }

        Result<Edital> editalResult = Edital.EmitirAbertura(Id, dados, clock);
        if (editalResult.IsFailure)
        {
            return Result<PublicacaoResultado>.Failure(editalResult.Error!);
        }

        Edital edital = editalResult.Value!;

        SnapshotPublicacao snapshot = SnapshotPublicacao.Congelar(
            edital.Id,
            configuracaoCongeladaCanonica,
            schemaVersion,
            algoritmoHash,
            hashEdital,
            atorUsuarioSub,
            clock);

        _editais.Add(edital);
        Status = StatusProcesso.Publicado;

        AddDomainEvent(new ProcessoPublicadoEvent(
            Id,
            edital.Id,
            snapshot.Id,
            snapshot.HashConfiguracao,
            snapshot.HashEdital,
            edital.DataPublicacao!.Value));

        return Result<PublicacaoResultado>.Success(new PublicacaoResultado(edital, snapshot));
    }

    /// <summary>
    /// Retifica um processo já publicado (RN08, Story #759 T5 #786, ADR-0101):
    /// emite um novo <see cref="Edital"/> de natureza retificação, vinculado ao
    /// Edital vigente da cadeia e com motivo obrigatório, e congela um novo
    /// <see cref="SnapshotPublicacao"/> — o snapshot anterior permanece
    /// intocado (append-only). O status continua Publicado. Os bytes canônicos
    /// já vêm do <c>ISnapshotPublicacaoCanonicalizer</c> (Application) com o
    /// bloco de retificação incluído; esta raiz não os produz (ADR-0042).
    /// </summary>
    /// <param name="editalRetificadoId">Edital vigente (topo da cadeia) que esta retificação sucede — deve pertencer a este processo (CA-06).</param>
    /// <param name="motivo">Justificativa obrigatória do ato de retificação (ADR-0101).</param>
    public Result<PublicacaoResultado> Retificar(
        DadosEdital dados,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashEdital,
        string atorUsuarioSub,
        Guid editalRetificadoId,
        string motivo,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(configuracaoCongeladaCanonica);
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != StatusProcesso.Publicado)
        {
            return Result<PublicacaoResultado>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {Status}."));
        }

        // CA-06 + ADR-0101: a retificação não cruza processos E sucede o
        // Edital VIGENTE (topo da cadeia — maior data de publicação, único
        // por ux_editais_processo_data_publicacao). Referenciar um edital de
        // outro processo, ou um nó que não é o topo, ramificaria a cadeia que
        // o seletor de snapshot vigente (T6) espera linear.
        Edital? vigente = _editais
            .Where(static e => e.DataPublicacao is not null)
            .OrderByDescending(static e => e.DataPublicacao!.Value)
            .FirstOrDefault();
        if (vigente is null || vigente.Id != editalRetificadoId)
        {
            return Result<PublicacaoResultado>.Failure(new DomainError(
                "ProcessoSeletivo.EditalRetificadoInvalido",
                "A retificação deve referenciar o Edital vigente (o mais recente) deste processo."));
        }

        Result<Edital> editalResult = Edital.EmitirRetificacao(Id, dados, editalRetificadoId, motivo, clock);
        if (editalResult.IsFailure)
        {
            return Result<PublicacaoResultado>.Failure(editalResult.Error!);
        }

        Edital edital = editalResult.Value!;

        SnapshotPublicacao snapshot = SnapshotPublicacao.Congelar(
            edital.Id,
            configuracaoCongeladaCanonica,
            schemaVersion,
            algoritmoHash,
            hashEdital,
            atorUsuarioSub,
            clock);

        _editais.Add(edital);

        // Reaproveita ProcessoPublicadoEvent (não um evento distinto): o fato
        // de negócio drenado é "emitido novo Edital + snapshot", idêntico em
        // forma ao da abertura — o payload (edital/snapshot/hashes/data) serve
        // aos dois. Evita um segundo schema Avro/tópico sem consumidor.
        AddDomainEvent(new ProcessoPublicadoEvent(
            Id,
            edital.Id,
            snapshot.Id,
            snapshot.HashConfiguracao,
            snapshot.HashEdital,
            edital.DataPublicacao!.Value));

        return Result<PublicacaoResultado>.Success(new PublicacaoResultado(edital, snapshot));
    }

    /// <summary>
    /// Trava de mutação pós-publicação (CA-04): todo <c>Definir*</c> recusa
    /// quando o processo já foi publicado — mudança em conteúdo congelável só
    /// via retificação (T5, #786). Reaproveitada por todos os métodos
    /// <c>Definir*</c>; <see langword="null"/> quando a mutação é permitida.
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

/// <summary>
/// Resultado de <see cref="ProcessoSeletivo.Publicar"/> — o <see cref="Edital"/>
/// e o <see cref="SnapshotPublicacao"/> recém-criados, para o handler
/// persistir via repositório sem precisar recriá-los ou tocar a coleção
/// interna do agregado.
/// </summary>
public sealed record PublicacaoResultado(Edital Edital, SnapshotPublicacao Snapshot);
