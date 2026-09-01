namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Enums;

using Events;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Services;
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
    /// <summary>Id da origem em Configuração, sem FK cross-schema (ADR-0061).</summary>
    public Guid TipoProcessoOrigemId => TipoProcesso.OrigemId;

    /// <summary>Snapshot-copy do tipo escolhido na criação, imutável para a vida do processo.</summary>
    public TipoProcessoSnapshot TipoProcesso { get; private set; } = null!;

    /// <summary>Alias de compatibilidade interna para fixtures; o contrato HTTP expõe <see cref="TipoProcesso"/>.</summary>
    public TipoProcessoSnapshot Tipo => TipoProcesso;
    public StatusProcesso Status { get; private set; }

    /// <summary>
    /// De onde vêm os candidatos deste certame (§3.4, Story #851) — NOT NULL, exigido na
    /// criação. Deriva o piso mínimo do cronograma de fases; nunca ramifica por
    /// <see cref="TipoProcesso"/>.
    /// </summary>
    public OrigemCandidatos OrigemCandidatos { get; private set; }

    /// <summary>
    /// Quem responde pelo certame (CA-04 da Feature #40; issue #849) — NOT NULL, exigido na
    /// criação, imutável depois (nenhum <c>Definir*</c> a altera; não há operação de
    /// re-bind). Escalar de topo, sem FK cross-schema — resolvido uma única vez via
    /// <c>IUnidadeReader</c> (ADR-0056) no momento da criação.
    /// </summary>
    public Guid UnidadeAdministradoraOrigemId { get; private set; }

    /// <summary>Snapshot-copy (ADR-0061) da Unidade administradora, congelado na criação — ver <see cref="UnidadeAdministradoraOrigemId"/>.</summary>
    public UnidadeAdministradoraSnapshot UnidadeAdministradora { get; private set; } = null!;

    /// <summary>
    /// Município cujo calendário rege a contagem dos prazos do certame (UNI-REQ-0111).
    /// Declarado na criação e alterável enquanto a configuração admite mutação — não é
    /// derivado da Unidade administradora, cuja cidade descreve onde ela fica, não sob
    /// que calendário o certame corre.
    /// </summary>
    public LocalidadeRegente Localidade { get; private set; } = null!;

    /// <summary>
    /// Convenção de contagem que o certame usa para os prazos que distinguem dia útil de
    /// dia não útil (UNI-REQ-0112): a entrada do rol de regras, congelada pela identidade
    /// <c>(codigo, versao, hash)</c> que reproduz a definição. Ausência = não declarada,
    /// o que só impede publicar quando alguma contagem depende dela.
    /// </summary>
    /// <remarks>
    /// É <b>uma por processo</b>, não uma por fase: um mesmo certame conta todos os seus
    /// prazos pela mesma convenção. Declará-la por fase permitiria que duas fases do
    /// mesmo edital fechassem a janela por leituras diferentes do que é um dia útil.
    /// </remarks>
    public ReferenciaRegra? AlgoritmoContagemPrazo { get; private set; }

    /// <summary>Título do formulário de inscrição apresentado ao candidato (Story #559). Ausência = sem título configurado.</summary>
    public string? FormularioTitulo { get; private set; }

    /// <summary>Texto do termo de aceite do formulário de inscrição (Story #559). Ausência = sem termo configurado.</summary>
    public string? FormularioTermoAceiteTexto { get; private set; }

    private readonly List<EtapaProcesso> _etapas = [];
    public IReadOnlyCollection<EtapaProcesso> Etapas => _etapas.AsReadOnly();

    /// <summary>Cronograma de fases do certame (1..*, Story #851) — o eixo temporal, distinto das <see cref="Etapas"/> (eixo de pontuação).</summary>
    private readonly List<FaseCronograma> _cronogramaFases = [];
    public IReadOnlyCollection<FaseCronograma> CronogramaFases => _cronogramaFases.AsReadOnly();

    /// <summary>Documentos exigidos do certame (0..*, Story #554) — por fase, aplicabilidade e gatilho.</summary>
    private readonly List<DocumentoExigido> _documentosExigidos = [];
    public IReadOnlyCollection<DocumentoExigido> DocumentosExigidos => _documentosExigidos.AsReadOnly();

    /// <summary>
    /// Árvore de satisfação dos documentos exigidos (0..*, Story #920) — coleção PLANA de
    /// TODOS os nós (não só raízes; ver <see cref="NoExigenciaConfiguration"/>). Substitui o
    /// antigo <c>DocumentoExigido.GrupoSatisfacaoId</c> (grupo plano, residual). Use
    /// <see cref="RaizesDeExigencia"/> para as raízes da floresta.
    /// </summary>
    private readonly List<FatoColetado> _fatosColetados = [];

    /// <summary>
    /// Os fatos que este processo coleta do candidato, com a ordem de coleta e a pré-condição de
    /// cada um (Story #926). Formam um grafo acíclico: a pré-condição de um fato só cita fatos
    /// anteriores na ordem.
    /// </summary>
    public IReadOnlyCollection<FatoColetado> FatosColetados => _fatosColetados.AsReadOnly();

    private readonly List<ConfiguracaoDerivacaoFato> _regrasDerivacao = [];

    /// <summary>
    /// As regras de derivação dos fatos derivados deste processo, por código de fato (Story #927):
    /// a configuração <c>{quando, contribui}</c> que o motor usa para resolver, por exemplo,
    /// <c>MODALIDADE</c> a partir dos fatos declarados.
    /// </summary>
    public IReadOnlyCollection<ConfiguracaoDerivacaoFato> RegrasDerivacao => _regrasDerivacao.AsReadOnly();

    private readonly List<NoExigencia> _nosExigencia = [];
    public IReadOnlyCollection<NoExigencia> NosExigencia => _nosExigencia.AsReadOnly();

    /// <summary>As raízes da floresta de árvores de satisfação — projeção em memória de <see cref="NosExigencia"/> (<c>NoPaiId == null</c>).</summary>
    public IEnumerable<NoExigencia> RaizesDeExigencia => _nosExigencia.Where(static n => n.NoPaiId is null);

    /// <summary>Âncora que resolve FAIXA_ETARIA na publicação (Story #554, PR #896) — ausência = nenhum gatilho por idade pode existir (bloqueado na publicação, ver <see cref="PendenciaDaReferenciaTemporalFatos"/>).</summary>
    public ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; private set; }

    public OfertaAtendimentoEspecializado? OfertaAtendimento { get; private set; }

    private readonly List<ConfiguracaoDistribuicaoVagas> _distribuicaoVagas = [];
    public IReadOnlyCollection<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas => _distribuicaoVagas.AsReadOnly();

    /// <summary>Bônus regional (RN05) — ausência = sem bônus (toggle por presença, INV-B5).</summary>
    public ConfiguracaoBonusRegional? BonusRegional { get; private set; }

    /// <summary>
    /// A cascata de remanejamento das cotas reservadas (Story #575, RN-CASCATA-1..5) —
    /// ausência = nenhuma cascata configurada (toggle por presença, mesmo padrão de
    /// <see cref="BonusRegional"/>). Uma só por processo, nunca por oferta de curso
    /// (§2.2 da story) — a cobertura por oferta é validada em <see cref="PendenciaDaCascata"/>.
    /// </summary>
    public ConfiguracaoCascataRemanejamento? Cascata { get; private set; }

    private readonly List<CriterioDesempate> _criteriosDesempate = [];
    public IReadOnlyCollection<CriterioDesempate> CriteriosDesempate => _criteriosDesempate.AsReadOnly();

    /// <summary>Configuração de classificação (15º bloco canônico, Story #775) — compõe por referência a fórmula, precisão, eliminação e ordem de alocação.</summary>
    public ConfiguracaoClassificacao? Classificacao { get; private set; }

    /// <summary>
    /// Divulgação pública do certame (UNI-REQ-0050, issue #563) — ausência já é o default
    /// minimizado (só o número de inscrição), não uma escolha administrativa pendente
    /// (mesmo padrão de toggle por presença de <see cref="BonusRegional"/>).
    /// </summary>
    public ConfiguracaoDivulgacao? ConfiguracaoDivulgacao { get; private set; }

    /// <summary>
    /// Taxa de inscrição e isenção (issue #1112) — <see langword="null"/> significa "ainda não
    /// declarado" e BLOQUEIA a publicação (CA-01, ver <see cref="ItensEstruturaisDeConformidade"/>).
    /// Diferente de <see cref="BonusRegional"/>/<see cref="ConfiguracaoDivulgacao"/>, ausência
    /// aqui não é um estado válido de publicação — é uma dimensão obrigatória ainda não
    /// preenchida.
    /// </summary>
    public ConfiguracaoTaxaInscricao? ConfiguracaoTaxaInscricao { get; private set; }

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

    public static ProcessoSeletivo Criar(
        string nome,
        TipoProcessoSnapshot tipoProcesso,
        OrigemCandidatos origemCandidatos,
        Guid unidadeAdministradoraOrigemId,
        UnidadeAdministradoraSnapshot unidadeAdministradora,
        LocalidadeRegente localidade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (tipoProcesso is null)
        {
            throw new ArgumentException("Tipo do processo é obrigatório.", nameof(tipoProcesso));
        }

        if (origemCandidatos == OrigemCandidatos.Nenhuma)
        {
            throw new ArgumentException("Origem dos candidatos é obrigatória.", nameof(origemCandidatos));
        }

        if (unidadeAdministradoraOrigemId == Guid.Empty)
        {
            throw new ArgumentException("Unidade administradora é obrigatória.", nameof(unidadeAdministradoraOrigemId));
        }

        ArgumentNullException.ThrowIfNull(unidadeAdministradora);
        ArgumentNullException.ThrowIfNull(localidade);

        return new ProcessoSeletivo
        {
            Nome = nome.Trim(),
            TipoProcesso = tipoProcesso,
            OrigemCandidatos = origemCandidatos,
            UnidadeAdministradoraOrigemId = unidadeAdministradoraOrigemId,
            UnidadeAdministradora = unidadeAdministradora,
            Localidade = localidade,
            Status = StatusProcesso.Rascunho,
        };
    }

    /// <summary>
    /// Substitui integralmente as etapas pontuadas do processo. O caráter e o
    /// peso definem o divisor da média (<see cref="CalcularDivisorMedia"/>);
    /// a <c>Ordem</c> é só a posição de apresentação no envelope congelado
    /// (issue #1069) — não entra na fórmula da nota final.
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
        // critério apontando para uma etapa removida — desempate inexecutável.
        // Rejeita a troca de etapas em vez de silenciosamente invalidar o
        // desempate; o admin reconfigura o desempate primeiro.
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

        // Story #554/issue #892 (CA-03): um gatilho DNF com fato
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
    /// #773): uma <see cref="ConfiguracaoDistribuicaoVagas"/>
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

        // Story #554/issue #892 (CA-03): mesmo raciocínio do
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
    /// Define (ou remove) a configuração de divulgação pública do processo (UNI-REQ-0050,
    /// issue #563). Passar <see langword="null"/> remove a configuração explícita — o
    /// processo volta ao default minimizado, mesmo padrão de <see cref="DefinirBonusRegional"/>.
    /// </summary>
    public Result DefinirConfiguracaoDivulgacao(ConfiguracaoDivulgacao? configuracao, PrecondicaoIfMatch precondicao)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (configuracao is null)
        {
            ConfiguracaoDivulgacao = null;
            Rascunho?.IncrementarRevisao();
            return Result.Success();
        }

        configuracao.VincularProcesso(Id);
        ConfiguracaoDivulgacao = configuracao;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Define (ou remove) a taxa de inscrição e os fundamentos de isenção do processo (issue
    /// #1112). Passar <see langword="null"/> remove a declaração — o processo volta a "ainda
    /// não declarado" (CA-01), que BLOQUEIA a publicação, diferente do toggle de
    /// <see cref="DefinirBonusRegional"/> (onde ausência é estado publicável).
    /// </summary>
    public Result DefinirTaxaInscricao(ConfiguracaoTaxaInscricao? configuracao, PrecondicaoIfMatch precondicao)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (configuracao is null)
        {
            ConfiguracaoTaxaInscricao = null;
            Rascunho?.IncrementarRevisao();
            return Result.Success();
        }

        configuracao.VincularProcesso(Id);
        ConfiguracaoTaxaInscricao = configuracao;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Define (ou remove) a cascata de remanejamento do processo (Story #575).
    /// Passar <see langword="null"/> remove a cascata — a ausência da entidade já
    /// é o toggle "sem cascata configurada", mesmo padrão de <see cref="DefinirBonusRegional"/>.
    /// A forma (RN-CASCATA-4) e o vínculo com a regra versionada (RN-CASCATA-5) já
    /// foram validados antes de chegar aqui — pela factory de <see cref="ConfiguracaoCascataRemanejamento"/>
    /// e pelo handler da Application, respectivamente; este método só aplica a mutação
    /// protegida pela sessão editorial.
    /// </summary>
    public Result DefinirCascataRemanejamento(ConfiguracaoCascataRemanejamento? cascata, PrecondicaoIfMatch precondicao)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (cascata is null)
        {
            Cascata = null;
            Rascunho?.IncrementarRevisao();
            return Result.Success();
        }

        cascata.VincularProcesso(Id);
        Cascata = cascata;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente os critérios de desempate do processo (Story
    /// #774). Dimensão opcional (0..*): lista vazia
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
    /// (Story #775). Valida a invariante que depende de
    /// OUTRA dimensão do agregado: INV-B4 (todo <c>etapa_ref</c> de uma
    /// <c>ELIM-NOTA-MINIMA-ETAPA</c> deve existir entre as etapas do
    /// processo). As invariantes internas da própria configuração (INV-B8,
    /// limites de <c>NOpcoesAlocacao</c>, e a restrição de que
    /// <c>ELIM-CORTE-REDACAO</c>/<c>ELIM-ZERO-EM-AREA</c> exigem
    /// <see cref="ConfiguracaoClassificacao.BaseadoEmEnem"/>) já foram
    /// validadas em <see cref="ConfiguracaoClassificacao.Criar"/>.
    /// </summary>
    public Result DefinirClassificacao(ConfiguracaoClassificacao classificacao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(classificacao);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        foreach (RegraEliminacao regra in classificacao.RegrasEliminacao)
        {
            if (regra.Args is ArgsElimNotaMinimaEtapa notaMinima && !_etapas.Any(e => e.Id == notaMinima.EtapaRef))
            {
                return Result.Failure(new DomainError(
                    "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
                    $"A regra de eliminação referencia a etapa {notaMinima.EtapaRef}, que não existe neste processo (INV-B4)."));
            }
        }

        classificacao.VincularProcesso(Id);
        Classificacao = classificacao;
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Define (ou substitui) o título e o texto do termo de aceite do formulário de inscrição
    /// (Story #559) — mesmo padrão dos demais <c>Definir*</c>: <see cref="MutacaoBloqueada"/>
    /// primeiro, <see cref="Result"/> nunca exceção. Sem invariante cruzando outra dimensão do
    /// agregado: os dois campos só se relacionam com a apresentação, nunca com a estrutura do
    /// processo.
    /// </summary>
    private const int FormularioTituloMaxLength = 300;
    private const int FormularioTermoAceiteTextoMaxLength = 4000;

    public Result DefinirFormulario(string? titulo, string? termoAceiteTexto, PrecondicaoIfMatch precondicao)
    {
        // ADR-0110 D9: a precondição de concorrência precede a validação de payload —
        // o guard de mutação roda primeiro, não depois (mesma ordem observável de hoje).
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        Result validacao = ValidarCamposDoFormulario(titulo, termoAceiteTexto);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        FormularioTitulo = string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim();
        FormularioTermoAceiteTexto = string.IsNullOrWhiteSpace(termoAceiteTexto) ? null : termoAceiteTexto.Trim();
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Acumula toda violação de campo do formulário em vez de retornar na primeira
    /// (ADR-0125) — alinhado a <c>ProcessoSeletivoConfiguration</c> (varchar(300)/
    /// varchar(4000)): sem o limite aqui, um valor mais longo passa e só falha em
    /// <c>SaveChanges</c> com erro de banco em vez de 422.
    /// </summary>
    private static Result ValidarCamposDoFormulario(string? titulo, string? termoAceiteTexto)
    {
        List<FieldError> erros = [];

        if (titulo is not null && titulo.Trim().Length > FormularioTituloMaxLength)
        {
            erros.Add(new("titulo", new DomainError(
                "ProcessoSeletivo.FormularioTituloTamanho",
                $"Título do formulário deve ter no máximo {FormularioTituloMaxLength} caracteres.")));
        }

        if (termoAceiteTexto is not null && termoAceiteTexto.Trim().Length > FormularioTermoAceiteTextoMaxLength)
        {
            erros.Add(new("termoAceiteTexto", new DomainError(
                "ProcessoSeletivo.FormularioTermoAceiteTextoTamanho",
                $"Termo de aceite do formulário deve ter no máximo {FormularioTermoAceiteTextoMaxLength} caracteres.")));
        }

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
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

        // Story #554/issue #893 (CA-04): reconciliação por FaseCanonicaOrigemId.
        // FaseCanonicaOrigemId é a identidade ESTÁVEL
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
            if (perdeuPermiteComplementacao && ExisteConsequenciaPendenciaReenvioNaFase(antiga.Id))
            {
                return Result.Failure(new DomainError(
                    "FaseCronograma.PendenciaReenvioExigeComplementacao",
                    $"A fase '{antiga.Codigo}' (ordem {antiga.Ordem}) não pode perder PermiteComplementacao — é referenciada por uma exigência (folha ou grupo) com consequência PENDENCIA_REENVIO."));
            }

            // Uma fase SOBREVIVENTE (mesma Ordem) cujo extremo
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

        // Mesma lacuna do guard eager de
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

        // Mesmo casando por identidade estável, uma
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

        // Reconciliação por FaseCanonicaOrigemId — a
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
    /// Substitui integralmente a árvore de satisfação de documentos exigidos do processo
    /// (Story #554, PR #895; Story #920 — árvore E/OU substitui o grupo plano): mesmo
    /// padrão dos demais <c>Definir*</c> — <see cref="MutacaoBloqueada"/> primeiro,
    /// <see cref="Result"/> nunca exceção, substituição integral das DUAS coleções
    /// (<see cref="DocumentosExigidos"/>, as folhas, e <see cref="NosExigencia"/>, a árvore).
    /// </summary>
    /// <remarks>
    /// Valida aqui o que só a raiz consegue provar: cada <see cref="DocumentoExigido.ExigidoNaFaseId"/>
    /// referencia uma fase viva do cronograma do MESMO processo (§2 da issue #547), e o gate
    /// <c>PENDENCIA_REENVIO</c>×<c>PermiteComplementacao</c> (forward — Story #920, fecha
    /// lacuna que só existia no sentido reverso em <see cref="DefinirCronogramaFases"/>) para
    /// folha OU grupo <c>OU</c>/<c>N-de</c>. Os invariantes da árvore em si (grupo não vazio,
    /// sem ciclo, mesma fase, cardinalidade por tipo de nó) já foram validados em
    /// <see cref="NoExigencia.CriarGrupo"/>, na montagem de <paramref name="raizes"/> — não
    /// revalidados aqui. O gatilho DNF (<c>CondicaoGatilho</c>, PR #896), a base legal
    /// (PR #898) e a idade/formato/tamanho (PR #900) não são tocados aqui.
    /// </remarks>

    public Result DefinirDocumentosExigidos(
        IReadOnlyList<NoExigencia> raizes,
        PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(raizes);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        List<NoExigencia> todosOsNos = [.. raizes.SelectMany(static raiz => raiz.AchatarComDescendentes())];
        List<DocumentoExigido> folhas = [.. todosOsNos
            .Where(static no => no.Tipo == TipoNo.Folha)
            .Select(static no => no.DocumentoExigido!)];

        foreach (DocumentoExigido item in folhas)
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
                // FIM_INSCRICAO não usa
                // ReferenciaFaseId (a âncora é implícita — a fase com ColetaInscricao do
                // PRÓPRIO cronograma, não uma fase escolhida pelo chamador), então o
                // branch acima nunca a valida. Sem esta checagem, um PUT aceitava a regra
                // num processo sem NENHUMA fase que coleta inscrição (ex.: importação
                // externa) ou com a fase de coleta sem Fim definido, deixando-a
                // irresolvível depois — mesmo sem gate de publicação para idade de
                // emissão (issue #893 §1: é aviso, não bloqueio de presença).
                // Nada no domínio impede MAIS de uma
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

        // Story #920: gate PENDENCIA_REENVIO×PermiteComplementacao FORWARD, para folha e
        // grupo OU/N-de — fecha lacuna que só existia no sentido reverso (DefinirCronogramaFases).
        foreach (NoExigencia no in todosOsNos)
        {
            string? consequenciaDoNo = no.Tipo == TipoNo.Folha
                ? no.DocumentoExigido!.ConsequenciaIndeferimento
                : no.Consequencia;

            if (consequenciaDoNo != "PENDENCIA_REENVIO")
            {
                continue;
            }

            Guid? faseId = no.FaseComum();
            if (faseId is { } faseIdValor && ValidarConsequenciaPendenciaReenvio(faseIdValor) is { } erroPendenciaReenvio)
            {
                return Result.Failure(erroPendenciaReenvio);
            }
        }

        _documentosExigidos.Clear();
        foreach (DocumentoExigido item in folhas)
        {
            item.VincularProcesso(Id);
            _documentosExigidos.Add(item);
        }

        _nosExigencia.Clear();
        foreach (NoExigencia raiz in raizes)
        {
            raiz.VincularProcesso(Id);
        }

        foreach (NoExigencia no in todosOsNos)
        {
            _nosExigencia.Add(no);
        }

        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente o grafo de coleta de fatos: quais fatos este processo coleta, em
    /// que ordem, e sob qual pré-condição cada campo é apresentado (Story #926).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Os invariantes fazem cumprir a norma de que uma pré-condição só cita <b>fatos
    /// anteriores</b>. Isso é mais estrito do que exigir apenas aciclicidade: um grafo pode ser
    /// acíclico e ainda assim ter um fato citando outro que vem depois dele na ordem de coleta —
    /// o que produziria um formulário em que a pergunta depende de uma resposta ainda não dada.
    /// </para>
    /// <para>
    /// A checagem de ordem sozinha já implica aciclicidade, porque um ciclo exigiria um fato com
    /// ordem menor que a de si mesmo. A detecção de ciclo é feita mesmo assim, e antes: ela
    /// devolve o <b>caminho</b> do ciclo, que é o que um administrador precisa para corrigir a
    /// configuração — enquanto o erro de ordem apontaria só um par de fatos.
    /// </para>
    /// <para>
    /// Editável em <b>rascunho</b> (pré-publicação) e sob <b>sessão de retificação</b> de um
    /// processo publicado — o mesmo padrão dos demais <c>Definir*</c>: <see cref="MutacaoBloqueada"/>
    /// primeiro, e a revisão do rascunho da sessão é incrementada ao final. Em rascunho puro não há
    /// sessão nem ETag (a precondição é ignorada); sob sessão, a precondição de concorrência é
    /// obrigatória. Os fatos coletados já participam do congelamento no envelope e da restauração
    /// da configuração (Story #928, §7.4), então o descarte da sessão repõe fielmente a coleta
    /// congelada — por isso a edição sob retificação é segura.
    /// </para>
    /// </remarks>
    public Result DefinirFatosColetados(IReadOnlyList<FatoColetado> fatosColetados, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(fatosColetados);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (ValidarGrafoDeFatos(fatosColetados) is { } erro)
        {
            return Result.Failure(erro);
        }

        _fatosColetados.Clear();
        foreach (FatoColetado fato in fatosColetados)
        {
            fato.VincularProcessoSeletivo(Id);
            _fatosColetados.Add(fato);
        }

        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Substitui integralmente as regras de derivação dos fatos derivados do processo (Story #927).
    /// </summary>
    /// <remarks>
    /// Editável em rascunho (pré-publicação) e sob sessão de retificação de um processo publicado —
    /// o mesmo padrão da coleta de fatos: <see cref="MutacaoBloqueada"/> primeiro, revisão do
    /// rascunho da sessão incrementada ao final. As regras já entram no envelope congelado e na
    /// restauração (Story #928, §7.4), então o descarte da sessão as repõe fielmente. A validação
    /// aqui é estrutural — código de fato único no processo. A validação semântica (o fato alvo é
    /// derivado com binding de regra; fatos citados e código contribuído no vocabulário e no
    /// domínio) depende de dados cross-módulo e é do comando na Application.
    /// </remarks>
    public Result DefinirRegrasDerivacao(IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(regrasDerivacao);

        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        HashSet<string> codigos = new(StringComparer.Ordinal);
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            if (!codigos.Add(config.CodigoFato))
            {
                return Result.Failure(new DomainError(
                    ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado,
                    $"O fato '{config.CodigoFato}' tem mais de uma configuração de derivação neste processo."));
            }
        }

        _regrasDerivacao.Clear();
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            config.VincularProcessoSeletivo(Id);
            _regrasDerivacao.Add(config);
        }

        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Limpa a coleta de fatos e as regras de derivação como passo da <b>restauração fiel</b> no
    /// descarte de uma retificação (Story #986). São as duas coleções que a edição sob retificação
    /// tornou mutáveis: uma sessão pode ter trocado ordens (0↔1) ou alterado pré-condições/regras,
    /// e repor as instâncias congeladas por cima das vivas colidiria no índice único de
    /// <c>Ordem</c>/código na mesma transação. A orquestração do descarte chama este método, faz um
    /// <c>SaveChanges</c> intermediário (os <c>DELETE</c>s saem primeiro) e só então aplica o grafo
    /// congelado (<c>INSERT</c> das instâncias reidratadas) — a reposição fiel de graça, sem
    /// reconciliação profunda dos filhos. Nenhuma identidade precisa sobreviver: são
    /// <c>EntityBase</c> puros, sem soft-delete, e o <c>Id</c> não é congelado no envelope nem
    /// referenciado por FK externa.
    /// </summary>
    public void LimparColetaEDerivacaoParaRestauracao()
    {
        _fatosColetados.Clear();
        _regrasDerivacao.Clear();
    }

    /// <summary>
    /// Monta o grafo de dependência conjunto (Story #928, §6) a partir das três dimensões da
    /// configuração que o alimentam — os fatos coletados (campo + fato + pré-condição), as regras de
    /// derivação (fato derivado + dependências) e as exigências (gatilho) — e valida a sua
    /// aciclicidade sobre as quatro classes de aresta juntas. Projeção read-only, sem mutar o
    /// agregado: um ciclo volta como erro nomeado, nunca lança. O congelamento do grafo no envelope e
    /// a recusa de publicação por ciclo são da fatia de determinismo (§7).
    /// </summary>
    public Result<GrafoDependenciaConjunta> ConstruirGrafoDependencia() =>
        GrafoDependenciaConjunta.Construir(_fatosColetados, _regrasDerivacao, _documentosExigidos);

    private static DomainError? ValidarGrafoDeFatos(IReadOnlyList<FatoColetado> fatos)
    {
        Dictionary<string, FatoColetado> porCodigo = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];

        foreach (FatoColetado fato in fatos)
        {
            if (!porCodigo.TryAdd(fato.FatoCodigo, fato))
            {
                return new DomainError(
                    FatoColetadoErrorCodes.FatoDuplicado,
                    $"O fato '{fato.FatoCodigo}' aparece mais de uma vez na coleta.");
            }

            if (!ordens.Add(fato.Ordem))
            {
                return new DomainError(
                    FatoColetadoErrorCodes.OrdemDuplicada,
                    $"A ordem {fato.Ordem} é usada por mais de um fato — a ordem de coleta precisa ser total.");
            }
        }

        // Ciclo antes de ordem: o erro de ciclo nomeia o caminho inteiro, que é acionável;
        // o de ordem apontaria só o primeiro par fora de sequência do mesmo problema.
        if (DetectarCiclo(porCodigo) is { } caminho)
        {
            return new DomainError(
                FatoColetadoErrorCodes.GrafoComCiclo,
                $"A pré-condição dos fatos forma um ciclo: {string.Join(" → ", caminho)}.");
        }

        foreach (FatoColetado fato in fatos)
        {
            foreach (string citado in fato.FatosCitados)
            {
                // A pré-condição de um campo só cita fato COLETADO — não um derivado. O resolvedor
                // de estado dos fatos (runtime) percorre apenas os fatos coletados por Ordem e não
                // aciona o motor de derivação; uma pré-condição que citasse um fato derivado
                // avaliaria indeterminada para sempre, e o campo nunca ficaria respondível. Enquanto
                // a coleta não for dirigida pelo grafo conjunto (§6, dependente do portador do
                // estado de coleta, ainda inexistente), a citação fica restrita ao que se coleta.
                if (!porCodigo.TryGetValue(citado, out FatoColetado? anterior))
                {
                    return new DomainError(
                        FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado,
                        $"A pré-condição do fato '{fato.FatoCodigo}' cita '{citado}', que este processo não coleta.");
                }

                if (anterior.Ordem >= fato.Ordem)
                {
                    return new DomainError(
                        FatoColetadoErrorCodes.PrecondicaoCitaFatoPosterior,
                        $"A pré-condição do fato '{fato.FatoCodigo}' (ordem {fato.Ordem}) cita '{citado}' "
                        + $"(ordem {anterior.Ordem}), que não é anterior — o campo dependeria de uma resposta ainda não dada.");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Busca em profundidade com marcação tricolor, devolvendo o caminho do primeiro ciclo
    /// encontrado — ou <see langword="null"/> quando o grafo é acíclico. A travessia segue, de
    /// cada fato, os fatos que a sua pré-condição <b>cita</b>, de modo que um caminho reportado
    /// <c>A → B → A</c> se lê "A cita B, B cita A".
    /// </summary>
    private static IReadOnlyList<string>? DetectarCiclo(Dictionary<string, FatoColetado> porCodigo)
    {
        HashSet<string> visitados = new(StringComparer.Ordinal);
        HashSet<string> naPilha = new(StringComparer.Ordinal);
        List<string> caminho = [];

        foreach (string codigo in porCodigo.Keys)
        {
            if (Visitar(codigo) is { } ciclo)
            {
                return ciclo;
            }
        }

        return null;

        IReadOnlyList<string>? Visitar(string codigo)
        {
            if (naPilha.Contains(codigo))
            {
                int inicio = caminho.IndexOf(codigo);
                return [.. caminho[inicio..], codigo];
            }

            if (!visitados.Add(codigo) || !porCodigo.TryGetValue(codigo, out FatoColetado? fato))
            {
                return null;
            }

            naPilha.Add(codigo);
            caminho.Add(codigo);

            foreach (string citado in fato.FatosCitados)
            {
                if (Visitar(citado) is { } ciclo)
                {
                    return ciclo;
                }
            }

            naPilha.Remove(codigo);
            caminho.RemoveAt(caminho.Count - 1);
            return null;
        }
    }

    /// <summary>
    /// Gate <c>PENDENCIA_REENVIO</c>×<c>PermiteComplementacao</c> (Story #920) — mesmo gate
    /// para folha e grupo, forward (aqui, na escrita da exigência) e reverso (em
    /// <see cref="DefinirCronogramaFases"/>, via <see cref="ExisteConsequenciaPendenciaReenvioNaFase"/>).
    /// </summary>
    private DomainError? ValidarConsequenciaPendenciaReenvio(Guid faseId)
    {
        FaseCronograma? fase = _cronogramaFases.FirstOrDefault(f => f.Id == faseId);
        if (fase is null)
        {
            // FaseNaoPertenceAoProcesso já cobre a ausência — defesa em profundidade, não
            // deveria ocorrer (a fase já foi validada acima nesta mesma chamada).
            return null;
        }

        return fase.PermiteComplementacao
            ? null
            : new DomainError(
                "DocumentoExigido.PendenciaReenvioExigeComplementacao",
                $"A fase '{fase.Codigo}' (ordem {fase.Ordem}) não permite complementação — consequência PENDENCIA_REENVIO exige PermiteComplementacao.");
    }

    /// <summary>Lado reverso do gate acima — usado por <see cref="DefinirCronogramaFases"/> ao verificar se uma fase pode perder <c>PermiteComplementacao</c>, para folha OU grupo <c>OU</c>/<c>N-de</c>.</summary>
    private bool ExisteConsequenciaPendenciaReenvioNaFase(Guid faseId) =>
        _documentosExigidos.Any(d => d.ExigidoNaFaseId == faseId && d.ConsequenciaIndeferimento == "PENDENCIA_REENVIO")
        || _nosExigencia.Any(n => n.Tipo == TipoNo.GrupoOu && n.Consequencia == "PENDENCIA_REENVIO" && n.FaseComum() == faseId);

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
    /// Redeclara o município cujo calendário rege a contagem dos prazos (UNI-REQ-0111), em rascunho
    /// ou sob sessão editorial aberta.
    /// </summary>
    /// <remarks>
    /// <para>A edição sob retificação só é segura porque a localidade agora participa do ciclo
    /// editorial inteiro: entra no envelope congelado, é lida pelo decoder e é reposta por
    /// <c>AplicarGrafo</c> no descarte. Enquanto o congelamento não existia, a escrita ficou
    /// restrita ao rascunho — liberá-la antes teria deixado uma edição descartada governando quais
    /// feriados incidem no prazo, e o fechamento publicaria os mesmos bytes apesar da configuração
    /// diferente.</para>
    /// <para>A atribuição é incondicional de propósito: como a igualdade de
    /// <see cref="LocalidadeRegente"/> considera só o código IBGE, comparar antes de
    /// atribuir descartaria silenciosamente a correção de um nome de exibição divergente,
    /// que é justamente o caso em que a redeclaração serve para consertar o rótulo.</para>
    /// </remarks>
    public Result DefinirLocalidade(LocalidadeRegente localidade, PrecondicaoIfMatch precondicao)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (localidade is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.LocalidadeAusente",
                "A localidade que rege a contagem dos prazos é obrigatória."));
        }

        Localidade = localidade;
        // Sem isto, alterar o município dentro da sessão não moveria o ETag, e uma escrita
        // concorrente com revisão velha continuaria sendo aceita.
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Declara a convenção de contagem que o certame usa nos prazos que distinguem dia
    /// útil (UNI-REQ-0112), em rascunho ou sob sessão editorial aberta.
    /// </summary>
    /// <remarks>
    /// A referência chega já resolvida contra o rol de regras: quem escolhe informa código
    /// e versão, e o handler troca isso pela identidade completa antes de chegar aqui. A
    /// raiz nunca monta a referência a partir do que veio na requisição — o <c>hash</c>
    /// ecoado do payload não provaria nada sobre a definição aplicada.
    /// </remarks>
    public Result DefinirAlgoritmoContagemPrazo(ReferenciaRegra algoritmo, PrecondicaoIfMatch precondicao)
    {
        if (MutacaoBloqueada(precondicao) is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (algoritmo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
                "A convenção de contagem dos prazos é obrigatória."));
        }

        AlgoritmoContagemPrazo = algoritmo;
        // Mesmo motivo do município: sem mover o ETag, trocar a convenção dentro da sessão
        // deixaria passar escrita concorrente com revisão velha.
        Rascunho?.IncrementarRevisao();
        return Result.Success();
    }

    /// <summary>
    /// Recusa gerar versão sem a localidade que rege a contagem dos prazos (UNI-REQ-0111).
    /// </summary>
    /// <remarks>
    /// A localidade é exigida desde a criação, então chegar aqui sem ela não é estado
    /// alcançável pelo fluxo público — a recusa fica como invariante da raiz, defendendo
    /// contra caminho de escrita que a contorne, e porque o requisito exige a recusa nomeada
    /// nas três transições que geram versão. É <b>também</b> a fonte única do item que
    /// <see cref="AvaliarConformidade"/> projeta: uma segunda condição escrita lá poderia
    /// divergir desta e devolver checklist verde para processo que a publicação recusa.
    /// </remarks>
    private DomainError? PendenciaDaLocalidade() =>
        Localidade is null
            ? new DomainError(
                "ProcessoSeletivo.LocalidadeAusente",
                "A localidade que rege a contagem dos prazos é obrigatória para gerar versão publicada.")
            : null;

    /// <summary>
    /// Recusa gerar versão quando alguma contagem do certame distingue dia útil e a
    /// convenção não foi declarada (UNI-REQ-0112/UNI-REQ-0116).
    /// </summary>
    /// <remarks>
    /// Basta existir regra de recurso para a dependência existir: toda regra declara prazo
    /// de interposição, e as duas unidades declaráveis nele correm sobre dia útil — dias
    /// úteis por definição, e horas porque só as horas situadas em dia útil avançam o
    /// relógio. A suspensividade em dias úteis depende igualmente, mas nunca sozinha, já
    /// que não existe suspensividade sem a regra que a carrega. Sem regra de recurso
    /// nenhuma, não há o que contar e a declaração não é exigida.
    /// </remarks>
    private DomainError? PendenciaDoAlgoritmoDeContagem()
    {
        if (AlgoritmoContagemPrazo is not null)
        {
            return null;
        }

        return AlgumaContagemDistingueDiaUtil()
            ? new DomainError(
                "ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
                "O processo tem prazo de recurso cuja contagem distingue dia útil, e não declarou a convenção de contagem que usa.")
            : null;
    }

    /// <summary>
    /// Recusa gerar versão quando alguma contagem do certame distingue dia útil e não há
    /// calendário de dias úteis vigente (UNI-REQ-0116).
    /// </summary>
    /// <remarks>
    /// Mesmo gatilho de <see cref="PendenciaDoAlgoritmoDeContagem"/>, e pela mesma razão: toda
    /// regra de recurso carrega prazo de interposição, e as duas unidades declaráveis nele
    /// correm sobre dia útil. A diferença é a origem do dado — a convenção o processo declara,
    /// o calendário vem do módulo Configuração e chega pelo contexto.
    /// <para>
    /// As duas causas são distintas e cada uma tem erro próprio: falta o dado que diz quais
    /// dias são úteis, ou falta a convenção que diz como contá-los. Podem faltar ao mesmo
    /// tempo, e reportar uma pela outra mandaria quem publica corrigir o lugar errado.
    /// </para>
    /// </remarks>
    private DomainError? PendenciaDoCalendarioVigente(ContextoDeContagemDePrazos contexto)
    {
        if (contexto.CalendarioVigente is not null)
        {
            return null;
        }

        if (!AlgumaContagemDistingueDiaUtil())
        {
            return null;
        }

        // Dataset vigente existe, mas veio incoerente do cadastro: a causa é essa, não a
        // ausência. Reportar "não há calendário" mandaria cadastrar um que já está lá.
        if (contexto.FalhaDoCalendarioVigente is { } falha)
        {
            return falha;
        }

        return AlgumaContagemDistingueDiaUtil()
            ? new DomainError(
                "ProcessoSeletivo.CalendarioVigenteAusente",
                "O processo tem prazo de recurso cuja contagem distingue dia útil, e não há calendário de dias úteis vigente cadastrado.")
            : null;
    }

    /// <summary>
    /// Se alguma contagem do certame distingue dia útil de não útil — o gatilho compartilhado
    /// pelos gates do calendário e da convenção de contagem (UNI-REQ-0116).
    /// </summary>
    /// <remarks>
    /// Basta existir regra de recurso: toda regra declara prazo de interposição, e as duas
    /// unidades declaráveis nele correm sobre dia útil — dias úteis por definição, e horas
    /// porque só as horas situadas em dia útil avançam o relógio. A suspensividade em dias
    /// úteis depende igualmente, mas nunca sozinha, já que não existe suspensividade sem a
    /// regra que a carrega. A janela de solicitação de isenção não é regra de recurso e não
    /// aciona nada disto.
    /// </remarks>
    private bool AlgumaContagemDistingueDiaUtil() =>
        _cronogramaFases.Exists(static fase => fase.RegraRecurso is not null);

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
    /// Os itens do PRIMEIRO gate estrutural — <see cref="PendenciaDeConformidade"/> (Story #758
    /// CA-07) — as dimensões estruturalmente OBRIGATÓRIAS do agregado: Oferta de atendimento
    /// especializado (1), Distribuição de vagas (1..*), Classificação (1) e Cronograma de fases
    /// (1..*, Story #851). Bônus regional (0..1) e critérios de desempate (0..*) são
    /// deliberadamente opcionais e NÃO entram — a ausência é um estado válido (RN05: ausência de
    /// bônus = sem bônus), não uma pendência.
    /// </summary>
    /// <remarks>
    /// <b>"Etapas" deixou de ser item incondicional (Story #851 §3.5).</b> Um processo
    /// sem prova (SiSU, <c>CLASSIFICACAO-IMPORTADA</c>) publica sem etapa — a fase que
    /// agrupa etapas existe se e somente se há etapa, e essa bicondicional é gate à
    /// parte (<see cref="PendenciaDoCronograma"/>), projetada em <see cref="AvaliarConformidade"/>
    /// por predicados próprios, não aqui. O que sobrevive aqui é a exigência de
    /// <see cref="RegraCalculoCodigo.FormulaMediaPonderada"/>: sob essa fórmula, o divisor da
    /// média (<see cref="CalcularDivisorMedia"/>) tem de ser maior que zero. Sob
    /// <see cref="RegraCalculoCodigo.ClassificacaoImportada"/>, a classificação dispensa etapa,
    /// fórmula e precisão locais — nenhum item aqui.
    /// <para>
    /// Fonte que <see cref="PendenciaDeConformidade"/> agrega no <c>DomainError</c> genérico
    /// (Story #575, achado de revisão de plano): a cascata de remanejamento tem erro NOMEADO
    /// próprio (<see cref="PendenciaDaCascata"/>) e não pode entrar nesta lista, senão o
    /// agregador genérico intercepta o erro específico antes que ele seja alcançado.
    /// </para>
    /// </remarks>
    private List<ItemConformidade> ItensEstruturaisDeConformidade()
    {
        List<ItemConformidade> itens =
        [
            new ItemConformidade("atendimento_especializado_ausente", DimensaoConformidade.AtendimentoEspecializado, "Atendimento especializado", OfertaAtendimento is not null),
            new ItemConformidade("distribuicao_vagas_ausente", DimensaoConformidade.DistribuicaoVagas, "Distribuição de vagas", _distribuicaoVagas.Count > 0),
            new ItemConformidade("classificacao_ausente", DimensaoConformidade.Classificacao, "Classificação", Classificacao is not null),
            new ItemConformidade("cronograma_fases_ausente", DimensaoConformidade.Cronograma, "Cronograma de fases", _cronogramaFases.Count > 0),
            // Issue #1112: publicar sem declarar cobrança de taxa é recusado — a ausência nunca
            // é interpretada como "não cobra" (CA-01). Diferente de BonusRegional/Divulgacao,
            // aqui ausência não é estado publicável.
            new ItemConformidade("taxa_inscricao_nao_declarada", DimensaoConformidade.TaxaInscricao, "Taxa de inscrição e isenção", ConfiguracaoTaxaInscricao is not null),
            // Issue #1310: processo que cobra reconhece ao menos um fundamento de isenção — a
            // possibilidade de pedir isenção se materializa nos fundamentos declarados. A fábrica
            // já recusa a combinação, mas ela é MATERIALIZÁVEL: o EF hidrata a linha direto da
            // coluna, sem passar por Criar, então configuração gravada antes da regra volta ao
            // agregado nesse estado. Sem este item, publicaria.
            // Verdadeiro (sem pendência) quando a configuração é nula: a ausência já é o item
            // acima, e dois vermelhos pela mesma causa fariam o operador procurar dois problemas.
            new ItemConformidade("taxa_inscricao_sem_fundamento_de_isencao", DimensaoConformidade.TaxaInscricao, "Fundamentos de isenção do processo que cobra taxa", TemFundamentoDeIsencaoQuandoCobra()),
            // Story #554, PR #898 (issue #549, ADR-0074): toda exigência que determina
            // resultado precisa de ≥1 base legal RESOLVIDO — semântica vazia quando não há
            // exigência que determine resultado (Services.ValidadorBaseLegalExigencias).
            // Story #920: estendido a grupo OU/N-de com consequência própria (exigência de
            // 1ª classe, base legal NÃO derivada dos filhos).
            new ItemConformidade(
                "exigencias_base_legal_nao_resolvida",
                DimensaoConformidade.ExigenciasDocumentais,
                "Base legal das exigências documentais",
                Services.ValidadorBaseLegalExigencias.TodasResolvidas(_documentosExigidos)
                    && GruposComConsequenciaTemBaseLegalResolvida()),
        ];

        if (Classificacao is { RegraCalculo.Codigo: RegraCalculoCodigo.FormulaMediaPonderada })
        {
            itens.Add(new ItemConformidade("classificacao_divisor_media_invalido", DimensaoConformidade.Classificacao, "Divisor da média (fórmula local)", CalcularDivisorMedia() > 0));
        }

        return itens;
    }

    /// <summary>
    /// Checklist de conformidade ESTRUTURAL do agregado (issue #1092): bicondicional com os
    /// SEIS gates que <see cref="Publicar"/>/<see cref="SucederVersao"/> aplicam, nesta ordem
    /// — <see cref="PendenciaDaLocalidade"/>, <see cref="PendenciaDoAlgoritmoDeContagem"/>,
    /// <see cref="PendenciaDeConformidade"/>, <see cref="PendenciaDoCronograma"/>,
    /// <see cref="PendenciaDaCascata"/> e <see cref="PendenciaPreCanonicalizacao"/>. Todos os
    /// itens ficam <see langword="true"/> se e somente se os seis gates não têm pendência —
    /// não existe estado em que este checklist declare tudo <c>Ok</c> e a publicação recuse por
    /// razão estrutural.
    /// </summary>
    /// <remarks>
    /// <b>Delimitação — "estrutural" não é "publicável".</b> Mesmo com os seis gates verdes, a
    /// publicação ainda pode recusar por conformidade LEGAL (motor data-driven, <c>GET
    /// /conformidade-legal</c>), documento confirmado, tipo de ato e outras leituras
    /// request-specific que só o command handler de publicação avalia (ADR-0109). Este método
    /// cobre só a publicabilidade ESTRUTURAL do agregado, nunca o ensaio completo do command
    /// handler.
    /// <para>
    /// <b>Sem segunda lista de predicados.</b> Cada item abaixo vem de um predicado privado
    /// NOMEADO que o gate correspondente TAMBÉM chama (os <c>Ha*</c> de
    /// <see cref="PendenciaDoCronograma"/>, os <c>Existe*</c> de <see cref="PendenciaDaCascata"/>
    /// e da coerência de indeferimento, os <c>ReferenciaTemporalFatos*</c>, e os sub-gates de
    /// <see cref="PendenciaPreCanonicalizacao"/> chamados diretamente) — o gate escolhe a
    /// PRIMEIRA falha na precedência que <see cref="Publicar"/> já fixa; este método projeta
    /// TODOS os vereditos. Uma razão nova acrescentada a um predicado compartilhado aparece nos
    /// dois ao mesmo tempo, por construção — não há um segundo <c>if</c> para lembrar de manter
    /// sincronizado.
    /// </para>
    /// <para>
    /// <b>Ordem estável e fiel aos gates.</b> A ordem dos itens é a de declaração abaixo,
    /// nunca a de iteração de uma coleção do EF — e reproduz a precedência que
    /// <see cref="Publicar"/> aplica. Quem lê o checklist de cima para baixo encontra a
    /// pendência que vai bloquear a publicação antes das que só apareceriam depois dela.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ItemConformidade> AvaliarConformidade(ContextoDeContagemDePrazos contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return
        [
        // ── Gates que a raiz aplica ANTES do agregador genérico, na ordem de Publicar ──
        // Os dois têm erro nomeado próprio e por isso não entram em
        // ItensEstruturaisDeConformidade(), que alimenta o agregador: lá dentro, o
        // ConformidadeInsuficiente interceptaria a causa específica antes de ela ser
        // alcançada — mesma razão pela qual a cascata fica de fora.
        new ItemConformidade("localidade_nao_declarada", DimensaoConformidade.ContagemDePrazos, "Localidade que rege a contagem dos prazos", PendenciaDaLocalidade() is null),
        new ItemConformidade("algoritmo_contagem_prazo_nao_declarado", DimensaoConformidade.ContagemDePrazos, "Convenção de contagem dos prazos de recurso", PendenciaDoAlgoritmoDeContagem() is null),
        new ItemConformidade("calendario_vigente_ausente", DimensaoConformidade.ContagemDePrazos, "Calendário de dias úteis vigente", PendenciaDoCalendarioVigente(contexto) is null),

        // O fuso não é gate de publicação — zona irresolvível é defeito de instalação, mapeado
        // para 500, e quem publica não tem o que corrigir. Projetá-lo assim mesmo é o que evita
        // que o preflight declare tudo pronto e a publicação devolva um 500 sem explicação.
        new ItemConformidade("fuso_institucional_nao_reconhecido", DimensaoConformidade.ContagemDePrazos, "Fuso institucional reconhecido pelo runtime", contexto.FusoInstitucionalReconhecido),

        .. ItensEstruturaisDeConformidade(),

        // ── PendenciaDoCronograma (Story #851 §3.4/§3.5) ──
        new ItemConformidade("cronograma_fase_agrupadora_sem_etapa_pontuada", DimensaoConformidade.Cronograma, "Cronograma: fase que agrupa etapas só quando há etapa pontuada", !HaFaseDeAvaliacaoSemEtapa()),
        new ItemConformidade("cronograma_etapa_pontuada_sem_fase_agrupadora", DimensaoConformidade.Cronograma, "Cronograma: etapa pontuada tem fase que agrupa etapas", !HaEtapaSemFaseDeAvaliacao()),
        new ItemConformidade("cronograma_inscricao_propria_sem_fase_de_coleta", DimensaoConformidade.Cronograma, "Cronograma: inscrição própria tem fase que coleta inscrição", !HaInscricaoPropriaSemFaseDeColeta()),
        new ItemConformidade("cronograma_fase_que_coleta_inscricao_sem_janela", DimensaoConformidade.Cronograma, "Cronograma: a fase que coleta inscrição tem início e fim definidos", FaseQueColetaInscricaoSemJanela() is null),
        new ItemConformidade("cronograma_vagas_sem_fase_que_produz_resultado", DimensaoConformidade.Cronograma, "Cronograma: vagas ofertadas têm fase que produz resultado", !HaVagasSemFaseQueProduzResultado()),

        // ── PendenciaDaCascata: o agregado e o detalhamento por razão (RN-CASCATA-1/2/2b/3, Story #575) ──
        new ItemConformidade("cascata_pendente", DimensaoConformidade.CascataRemanejamento, "Cascata de remanejamento", PendenciaDaCascata() is null),
        new ItemConformidade("cascata_modalidade_fora_do_regime_federal", DimensaoConformidade.CascataRemanejamento, "Cascata: modalidade SegueCascata usa a regra de distribuição federal", !ExisteCascataForaDoRegimeFederal()),
        new ItemConformidade("cascata_origem_ausente", DimensaoConformidade.CascataRemanejamento, "Cascata: origem SegueCascata declarada na cascata de remanejamento", !ExisteCascataOrigemAusente()),
        new ItemConformidade("cascata_fallback_nao_ofertado", DimensaoConformidade.CascataRemanejamento, "Cascata: fallback e destinos resolvíveis nas modalidades ofertadas", !ExisteCascataFallbackNaoSelecionadoNaOferta()),
        new ItemConformidade("cascata_origem_nao_segue_cascata", DimensaoConformidade.CascataRemanejamento, "Cascata: origem declarada corresponde a modalidade SegueCascata ofertada", !ExisteCascataOrigemNaoSegueCascata()),
        new ItemConformidade("cascata_destino_desconhecido", DimensaoConformidade.CascataRemanejamento, "Cascata: destino declarado corresponde a modalidade ofertada", !ExisteCascataDestinoDesconhecido()),

        // ── PendenciaPreCanonicalizacao, na mesma ordem do gate (Story #554/#920/#927/#928) ──
        new ItemConformidade("exigencia_condicional_vazia_determina_resultado", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: sem CONDICIONAL vazia que determina resultado", PendenciaDasExigenciasDocumentais() is null),
        new ItemConformidade("exigencia_remove_vantagem_sem_vantagem_viva", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: REMOVE_VANTAGEM com vantagem viva (exigência)", !ExisteExigenciaRemoveVantagemSemVantagemViva()),
        new ItemConformidade("exigencia_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: coerente com a ação da vaga (exigência)", !ExisteExigenciaConsequenciaIncoerenteComAcaoDaVaga()),
        new ItemConformidade("grupo_remove_vantagem_sem_vantagem_viva", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: REMOVE_VANTAGEM com vantagem viva (grupo)", !ExisteGrupoRemoveVantagemSemVantagemViva()),
        new ItemConformidade("grupo_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: coerente com a ação da vaga (grupo)", !ExisteGrupoConsequenciaIncoerenteComAcaoDaVaga()),
        new ItemConformidade("referencia_temporal_ausente_com_gatilho_etario", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: configurada quando há gatilho por faixa etária", !ReferenciaTemporalFatosAusenteQuandoExigida()),
        new ItemConformidade("referencia_temporal_fase_fora_do_cronograma", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: fase âncora pertence ao cronograma", !ReferenciaTemporalFatosFaseNaoPertenceAoCronograma()),
        new ItemConformidade("referencia_temporal_extremo_da_fase_ausente", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: extremo da fase âncora definido", !ReferenciaTemporalFatosExtremoDaFaseAusente()),
        new ItemConformidade("referencia_temporal_fim_inscricao_indisponivel", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: fase de coleta com Fim definido para FIM_INSCRICAO", !ReferenciaTemporalFatosFimInscricaoIndisponivel()),
        new ItemConformidade("derivacao_fatos_citados_inexistentes", DimensaoConformidade.ColetaDeFatos, "Regras de derivação: fatos citados existem no processo", PendenciaDeFatosCitados() is null),
        new ItemConformidade("fato_coletavel_sem_valores_ofertados", DimensaoConformidade.ColetaDeFatos, "Fato coletável de escopo do processo: oferta declara ao menos um valor", PendenciaDeFatoColetadoSemValoresOfertados() is null),
        new ItemConformidade("derivacao_dominio_de_contribuicao_invalido", DimensaoConformidade.ColetaDeFatos, "Regras de derivação: código contribuído pertence ao domínio ofertado", PendenciaDoDominioDeContribuicao() is null),
        new ItemConformidade("grafo_dependencia_com_ciclo", DimensaoConformidade.ColetaDeFatos, "Grafo de dependência conjunto: sem ciclo", PendenciaDoGrafoConjunto() is null),
        ];
    }

    /// <summary>
    /// Processo que cobra taxa reconhece ao menos um fundamento de isenção (issue #1310).
    /// Verdadeiro quando não há configuração declarada — a ausência é pendência do item anterior —
    /// e quando o processo declara não cobrar, caso em que fundamento nenhum é o estado correto
    /// (UNI-REQ-0100).
    /// </summary>
    /// <remarks>
    /// Contar elementos basta: token fora do vocabulário não chega até aqui como lista "cheia de
    /// nada". O conversor de <c>ConfiguracaoTaxaInscricaoConfiguration</c> LANÇA ao reserializar
    /// <see cref="FundamentoIsencao.Nenhum"/>, então uma coluna adulterada estoura na carga do
    /// agregado, alto e cedo, em vez de virar publicação silenciosa
    /// (<c>TaxaInscricaoSemFundamentoPersistenciaTests</c> prova as duas metades).
    /// </remarks>
    private bool TemFundamentoDeIsencaoQuandoCobra() =>
        ConfiguracaoTaxaInscricao is not { Cobra: true, Fundamentos.Count: 0 };

    /// <summary>Grupos <c>OU</c>/<c>N-de</c> com <see cref="NoExigencia.Consequencia"/> própria (Story #920) — cada um precisa de ≥1 <see cref="NoExigenciaBaseLegal"/> <see cref="StatusBaseLegal.Resolvido"/>, mesma semântica de <see cref="Services.ValidadorBaseLegalExigencias"/> para folha.</summary>
    private bool GruposComConsequenciaTemBaseLegalResolvida() =>
        _nosExigencia
            .Where(static no => no.DeterminaResultado())
            .All(no => no.BasesLegais.Any(static b => b.Status == StatusBaseLegal.Resolvido));

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
        // Story #575: só os itens ESTRUTURAIS entram no agregador genérico — a cascata de
        // remanejamento tem erro nomeado próprio (PendenciaDaCascata) e nunca passa por aqui.
        IReadOnlyList<ItemConformidade> pendencias = [.. ItensEstruturaisDeConformidade().Where(static item => !item.Ok)];
        if (pendencias.Count == 0)
        {
            return null;
        }

        return new DomainError(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            $"Processo não conforme para publicação — pendente: {string.Join(", ", pendencias.Select(static p => p.Mensagem))}.");
    }

    /// <summary>
    /// Pendência de cobertura da cascata de remanejamento (Story #575, RN-CASCATA-1/2/2b/3) —
    /// erro NOMEADO, público (chamado tanto pela raiz quanto pelos handlers da Application,
    /// que estão em outro assembly). Chamado por <see cref="Publicar"/> e <see cref="SucederVersao"/>
    /// na mesma posição de <see cref="PendenciaDoCronograma"/>.
    /// </summary>
    /// <remarks>
    /// A ordenação é determinística em todos os níveis — ofertas por <c>OfertaCursoOrigemId</c>
    /// (comparação direta de <see cref="Guid"/>), modalidades de cada oferta por <c>Codigo</c>
    /// (<see cref="StringComparer.Ordinal"/>), origens da cascata por <c>ModalidadeOrigemCodigo</c>
    /// (ordinal) e destinos por <c>Ordem</c> — para que o primeiro erro nunca varie conforme a
    /// ordem física de retorno do Postgres (os <c>Include</c> de <c>ComConfiguracao</c> não
    /// aplicam ordem às coleções).
    /// </remarks>
    public DomainError? PendenciaDaCascata()
    {
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas = [.. _distribuicaoVagas.OrderBy(static o => o.OfertaCursoOrigemId)];

        foreach (ConfiguracaoDistribuicaoVagas oferta in ofertas)
        {
            IReadOnlyList<ModalidadeSelecionada> modalidadesSegueCascata = [.. oferta.Modalidades
                .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
                .OrderBy(static m => m.Codigo, StringComparer.Ordinal)];

            if (modalidadesSegueCascata.Count == 0)
            {
                continue;
            }

            // RN-CASCATA-2b: SegueCascata só é coberta pela cascata única do processo quando a
            // oferta usa o regime federal — fora dele, a cascata não tem o que validar.
            if (OfertaForaDoRegimeFederal(oferta))
            {
                return new DomainError(
                    "ProcessoSeletivo.CascataForaDoRegimeFederal",
                    $"A oferta {oferta.OfertaCursoOrigemId} tem modalidade \"{modalidadesSegueCascata[0].Codigo}\" com SegueCascata, mas não usa a regra de distribuição {RegraDistribuicaoVagasCodigo.Lei12711}.");
            }

            if (Cascata is null)
            {
                return new DomainError(
                    "ProcessoSeletivo.CascataOrigemAusente",
                    $"A oferta {oferta.OfertaCursoOrigemId} tem modalidade \"{modalidadesSegueCascata[0].Codigo}\" com SegueCascata, mas o processo não tem cascata de remanejamento configurada.");
            }

            if (FallbackNaoSelecionadoNaOferta(oferta, Cascata.FallbackCodigo))
            {
                return new DomainError(
                    "ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta",
                    $"O fallback \"{Cascata.FallbackCodigo}\" da cascata não é uma modalidade selecionada na oferta {oferta.OfertaCursoOrigemId}.");
            }

            foreach (ModalidadeSelecionada modalidade in modalidadesSegueCascata)
            {
                if (OrigemNaoDeclaradaNaCascata(modalidade.Codigo))
                {
                    return new DomainError(
                        "ProcessoSeletivo.CascataOrigemAusente",
                        $"A oferta {oferta.OfertaCursoOrigemId} tem modalidade \"{modalidade.Codigo}\" com SegueCascata, mas a cascata não declara nenhum destino para ela.");
                }

                if (DestinoDaOrigemNaoResolvivelNaOferta(oferta, modalidade.Codigo))
                {
                    return new DomainError(
                        "ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta",
                        $"Nenhum destino da origem \"{modalidade.Codigo}\" na cascata é uma modalidade selecionada na oferta {oferta.OfertaCursoOrigemId}.");
                }
            }
        }

        if (Cascata is null)
        {
            return null;
        }

        HashSet<string> todosOsCodigosOfertados = new(
            ofertas.SelectMany(static o => o.Modalidades).Select(static m => m.Codigo),
            StringComparer.Ordinal);
        HashSet<string> todasAsOrigensSegueCascata = new(
            ofertas.SelectMany(static o => o.Modalidades)
                .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
                .Select(static m => m.Codigo),
            StringComparer.Ordinal);

        IReadOnlyList<DestinoRemanejamento> origensDaCascataEmOrdem = [.. Cascata.Destinos
            .Select(static d => d.ModalidadeOrigemCodigo)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static o => o, StringComparer.Ordinal)
            .SelectMany(origem => Cascata.Destinos
                .Where(d => string.Equals(d.ModalidadeOrigemCodigo, origem, StringComparison.Ordinal))
                .OrderBy(static d => d.Ordem))];

        foreach (DestinoRemanejamento destino in origensDaCascataEmOrdem)
        {
            if (!todasAsOrigensSegueCascata.Contains(destino.ModalidadeOrigemCodigo))
            {
                return new DomainError(
                    "ProcessoSeletivo.CascataOrigemNaoSegueCascata",
                    $"A cascata declara a origem \"{destino.ModalidadeOrigemCodigo}\", mas nenhuma oferta do processo a marca como SegueCascata.");
            }

            if (!todosOsCodigosOfertados.Contains(destino.ModalidadeDestinoCodigo))
            {
                return new DomainError(
                    "ProcessoSeletivo.CascataDestinoDesconhecido",
                    $"A cascata declara o destino \"{destino.ModalidadeDestinoCodigo}\", que não é modalidade selecionada em nenhuma oferta do processo.");
            }
        }

        return null;
    }

    /// <summary>A oferta exige a cascata única do processo: tem ≥1 modalidade SegueCascata (RN-CASCATA-2b).</summary>
    private static bool OfertaExigeCascata(ConfiguracaoDistribuicaoVagas oferta) =>
        oferta.Modalidades.Any(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata);

    /// <summary>A oferta não usa o regime federal (Lei 12.711/2012) — condição de <c>CascataForaDoRegimeFederal</c>.</summary>
    private static bool OfertaForaDoRegimeFederal(ConfiguracaoDistribuicaoVagas oferta) =>
        oferta.RegraDistribuicao.Codigo != RegraDistribuicaoVagasCodigo.Lei12711;

    /// <summary>O fallback da cascata não é modalidade selecionada na oferta — condição de <c>CascataFallbackNaoSelecionadoNaOferta</c> (nível oferta).</summary>
    private static bool FallbackNaoSelecionadoNaOferta(ConfiguracaoDistribuicaoVagas oferta, string fallbackCodigo) =>
        !oferta.Modalidades.Select(static m => m.Codigo).Contains(fallbackCodigo, StringComparer.Ordinal);

    /// <summary>A cascata não declara nenhum destino para a origem — condição de <c>CascataOrigemAusente</c> (nível modalidade).</summary>
    private bool OrigemNaoDeclaradaNaCascata(string modalidadeOrigemCodigo) =>
        Cascata is not null
        && !Cascata.Destinos.Any(d => string.Equals(d.ModalidadeOrigemCodigo, modalidadeOrigemCodigo, StringComparison.Ordinal));

    /// <summary>Nenhum destino declarado da origem é modalidade selecionada NESTA oferta — condição de <c>CascataFallbackNaoSelecionadoNaOferta</c> (nível modalidade).</summary>
    private bool DestinoDaOrigemNaoResolvivelNaOferta(ConfiguracaoDistribuicaoVagas oferta, string modalidadeOrigemCodigo)
    {
        if (Cascata is null)
        {
            return false;
        }

        HashSet<string> codigosDaOferta = new(oferta.Modalidades.Select(static m => m.Codigo), StringComparer.Ordinal);
        return !Cascata.Destinos
            .Where(d => string.Equals(d.ModalidadeOrigemCodigo, modalidadeOrigemCodigo, StringComparison.Ordinal))
            .OrderBy(static d => d.Ordem)
            .Any(d => codigosDaOferta.Contains(d.ModalidadeDestinoCodigo));
    }

    /// <summary>
    /// Existe alguma oferta com modalidade SegueCascata sob regime não-federal — mesmo predicado
    /// de <see cref="OfertaForaDoRegimeFederal"/> que o gate usa, agregado sobre TODAS as ofertas
    /// (não só a primeira) para o item do checklist estrutural.
    /// </summary>
    private bool ExisteCascataForaDoRegimeFederal() =>
        _distribuicaoVagas.Any(o => OfertaExigeCascata(o) && OfertaForaDoRegimeFederal(o));

    /// <summary>Existe alguma origem SegueCascata (cascata ausente, ou destino não declarado) alcançável por uma oferta federal.</summary>
    private bool ExisteCascataOrigemAusente()
    {
        foreach (ConfiguracaoDistribuicaoVagas oferta in _distribuicaoVagas)
        {
            if (!OfertaExigeCascata(oferta) || OfertaForaDoRegimeFederal(oferta))
            {
                continue;
            }

            if (Cascata is null)
            {
                return true;
            }

            if (oferta.Modalidades
                .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
                .Any(m => OrigemNaoDeclaradaNaCascata(m.Codigo)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Existe fallback ou destino não resolvível numa oferta federal que exige cascata.</summary>
    private bool ExisteCascataFallbackNaoSelecionadoNaOferta()
    {
        foreach (ConfiguracaoDistribuicaoVagas oferta in _distribuicaoVagas)
        {
            if (!OfertaExigeCascata(oferta) || OfertaForaDoRegimeFederal(oferta) || Cascata is null)
            {
                continue;
            }

            if (FallbackNaoSelecionadoNaOferta(oferta, Cascata.FallbackCodigo))
            {
                return true;
            }

            if (oferta.Modalidades
                .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
                .Any(m => !OrigemNaoDeclaradaNaCascata(m.Codigo) && DestinoDaOrigemNaoResolvivelNaOferta(oferta, m.Codigo)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Existe origem declarada na cascata que nenhuma oferta do processo marca como SegueCascata.</summary>
    private bool ExisteCascataOrigemNaoSegueCascata()
    {
        if (Cascata is null)
        {
            return false;
        }

        HashSet<string> todasAsOrigensSegueCascata = new(
            _distribuicaoVagas.SelectMany(static o => o.Modalidades)
                .Where(static m => m.RegraRemanejamento == RegraRemanejamentoModalidade.SegueCascata)
                .Select(static m => m.Codigo),
            StringComparer.Ordinal);

        return Cascata.Destinos
            .Select(static d => d.ModalidadeOrigemCodigo)
            .Distinct(StringComparer.Ordinal)
            .Any(origem => !todasAsOrigensSegueCascata.Contains(origem));
    }

    /// <summary>Existe destino declarado na cascata que não é modalidade selecionada em nenhuma oferta do processo.</summary>
    private bool ExisteCascataDestinoDesconhecido()
    {
        if (Cascata is null)
        {
            return false;
        }

        HashSet<string> todosOsCodigosOfertados = new(
            _distribuicaoVagas.SelectMany(static o => o.Modalidades).Select(static m => m.Codigo),
            StringComparer.Ordinal);

        return Cascata.Destinos.Any(d => !todosOsCodigosOfertados.Contains(d.ModalidadeDestinoCodigo));
    }

    /// <summary>
    /// Pendências do cronograma que não cabem no checklist booleano ORIGINAL de
    /// <see cref="PendenciaDeConformidade"/> — cada uma tem o seu próprio <c>DomainError</c>
    /// nomeado (Story #851 §3.4/§3.5, CA-11/CA-13/CA-14). Chamado por
    /// <see cref="Publicar"/> e por <see cref="SucederVersao"/> (Retificar/FecharRetificacao),
    /// sempre <b>depois</b> de <see cref="PendenciaDeConformidade"/>.
    /// </summary>
    /// <remarks>
    /// Issue #1092: cada <c>if</c> abaixo testa um predicado privado nomeado
    /// (<c>Ha*</c>) em vez de uma expressão inline — <see cref="AvaliarConformidade"/>
    /// chama os MESMOS quatro predicados para projetar um item por razão. A precedência
    /// (qual falha é devolvida primeiro) continua decidida só aqui, pela ORDEM dos
    /// <c>if</c>; o checklist não reordena nem filtra — só projeta todos os quatro.
    /// </remarks>
    /// <remarks>
    /// Público, e não privado como os demais gates do cronograma: o handler o antecipa antes de
    /// canonicalizar (issue #1350). Sem isso a recusa da fase de coleta sem janela chegaria depois
    /// de PendenciaPreCanonicalizacao, e um processo que também tivesse referência temporal
    /// irresolvível receberia pelo endpoint a recusa da referência — diagnóstico obscuro para quem
    /// só precisa saber que falta datar a inscrição.
    /// </remarks>
    public DomainError? PendenciaDoCronograma()
    {
        // §3.5, direção "fase de avaliação sem etapa" — defesa em profundidade: o mesmo
        // sentido já é bloqueado eagerly em DefinirCronogramaFases, mas uma etapa
        // removida DEPOIS (via DefinirEtapas) deixaria uma fase de avaliação órfã sem
        // que nada a pegasse na hora — o gate de publicação é a rede de segurança.
        if (HaFaseDeAvaliacaoSemEtapa())
        {
            return new DomainError(
                "ProcessoSeletivo.AvaliacaoSemEtapa",
                "Há uma fase que agrupa etapas no cronograma, mas o processo não tem nenhuma etapa pontuada.");
        }

        // §3.5, direção "etapa sem fase de avaliação" — lazy por natureza (a etapa pode
        // ser declarada depois do cronograma).
        if (HaEtapaSemFaseDeAvaliacao())
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
                "O processo tem etapa pontuada, mas nenhuma fase do cronograma agrupa etapas.");
        }

        // §3.4 — piso mínimo derivado da ORIGEM DOS CANDIDATOS, nunca do tipo.
        if (HaInscricaoPropriaSemFaseDeColeta())
        {
            return new DomainError(
                "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
                "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma coleta inscrição.");
        }

        // Issue #1350: a fase que coleta inscrição declara o prazo que o Edital publica, então
        // ela precisa de janela mesmo quando a origem da data é DELEGADA. É a única exceção à
        // §3.2 — nas demais fases, "sem data" continua sendo estado válido, porque o setor
        // responsável não congela data que não controla. Um Edital que admite inscrição sem
        // prazo definido, esse não se sustenta.
        if (FaseQueColetaInscricaoSemJanela() is { } faseSemJanela)
        {
            return new DomainError(
                "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
                $"A fase '{faseSemJanela.Codigo}' coleta inscrição e precisa de início e fim definidos para que o Edital declare o período.");
        }

        // §3.4 — havendo vagas ofertadas, o cronograma precisa de ao menos uma fase que
        // produza resultado.
        if (HaVagasSemFaseQueProduzResultado())
        {
            return new DomainError(
                "ProcessoSeletivo.VagasSemFaseQueProduzResultado",
                "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado.");
        }

        return null;
    }

    /// <summary>Fase que agrupa etapas existe, mas o processo não tem etapa pontuada (§3.5).</summary>
    private bool HaFaseDeAvaliacaoSemEtapa() =>
        _cronogramaFases.Any(static f => f.AgrupaEtapas) && _etapas.Count == 0;

    /// <summary>Etapa pontuada existe, mas nenhuma fase do cronograma agrupa etapas (§3.5).</summary>
    private bool HaEtapaSemFaseDeAvaliacao() =>
        _etapas.Count > 0 && !_cronogramaFases.Any(static f => f.AgrupaEtapas);

    /// <summary>Origem InscricaoPropria sem nenhuma fase que colete inscrição (§3.4).</summary>
    /// <summary>
    /// A fase que ancora o período de inscrição quando ela existe e está sem janela (issue #1350)
    /// — <see langword="null"/> quando não há fase de coleta, ou quando a que há tem os dois
    /// extremos definidos.
    /// </summary>
    /// <remarks>
    /// Incide sobre a MESMA fase que <see cref="FaseQueAncoraOPeriodoDeInscricao"/> elege, e não
    /// sobre "alguma fase de coleta": fossem conjuntos diferentes, a projeção poderia tirar o
    /// período de uma fase enquanto a recusa olhasse outra.
    /// </remarks>
    private FaseCronograma? FaseQueColetaInscricaoSemJanela() =>
        FaseQueAncoraOPeriodoDeInscricao() is { } ancora && (ancora.Inicio is null || ancora.Fim is null)
            ? ancora
            : null;

    private bool HaInscricaoPropriaSemFaseDeColeta() =>
        OrigemCandidatos == OrigemCandidatos.InscricaoPropria && !_cronogramaFases.Any(static f => f.ColetaInscricao);

    /// <summary>Há vagas ofertadas (VoBase > 0), mas nenhuma fase do cronograma produz resultado (§3.4).</summary>
    private bool HaVagasSemFaseQueProduzResultado() =>
        _distribuicaoVagas.Any(static d => d.VoBase > 0) && !_cronogramaFases.Any(static f => f.ProduzResultado);

    /// <summary>
    /// Documentos exigidos, coerência de consequência e referência temporal de fatos —
    /// os três checáveis que <see cref="Publicar"/>/<see cref="SucederVersao"/> avaliam
    /// depois de <see cref="PendenciaDoCronograma"/>, agregados aqui para que o
    /// <b>handler</b> (Application) também os alcance <b>antes</b> de canonicalizar.
    /// </summary>
    /// <remarks>
    /// Achado de revisão (Story #554, PR #903, ADR-0109 D5): o gate de conformidade
    /// PRECEDE a canonicalização por design — <see cref="PendenciaDeConformidade"/> já é
    /// <see langword="public"/> e os handlers o chamam antes de
    /// <c>ISnapshotPublicacaoCanonicalizer.Canonicalizar</c> exatamente por isso. Mas
    /// <c>Canonicalizar</c> agora invoca <see cref="ResolverDataReferenciaFatos"/>
    /// internamente (serialização de <c>documentosExigidos</c>), e esse método
    /// <b>lança</b> quando a referência não resolve — sem este guard também rodando
    /// ANTES de canonicalizar, uma publicação/retificação inválida vira exceção não
    /// tratada (500) em vez do <see cref="DomainError"/> nomeado que
    /// <see cref="Publicar"/>/<see cref="SucederVersao"/> devolveriam depois, se a
    /// exceção não tivesse interrompido o fluxo antes.
    /// </remarks>
    public DomainError? PendenciaPreCanonicalizacao()
    {
        if (PendenciaDasExigenciasDocumentais() is { } exigencias)
        {
            return exigencias;
        }

        if (PendenciaDeCoerenciaDaConsequenciaDeIndeferimento() is { } coerencia)
        {
            return coerencia;
        }

        if (PendenciaDaReferenciaTemporalFatos() is { } referenciaTemporal)
        {
            return referenciaTemporal;
        }

        if (PendenciaDeFatosCitados() is { } fatoCitado)
        {
            return fatoCitado;
        }

        if (PendenciaDeFatoColetadoSemValoresOfertados() is { } fatoSemOferta)
        {
            return fatoSemOferta;
        }

        if (PendenciaDoDominioDeContribuicao() is { } contribuicaoForaDoDominio)
        {
            return contribuicaoForaDoDominio;
        }

        return PendenciaDoGrafoConjunto();
    }

    /// <summary>
    /// O código que a derivação de <c>MODALIDADE</c> contribui tem de pertencer ao domínio congelado
    /// daquele processo — o conjunto de modalidades ofertadas (Story #928, §7.2). Um código fora dele
    /// é recusado com erro nomeado, <b>sem tradução de alias</b>: <c>V</c>, por exemplo, é rótulo de
    /// exibição de <c>AC_PCD</c> no edital, nunca um código de entrada. A canonicidade é avaliada
    /// contra o domínio da configuração, não contra o conjunto federal global.
    /// </summary>
    /// <remarks>
    /// Reconstruir o VO da regra contra o domínio (<see cref="ConfiguracaoDerivacaoFato.ParaRegrasDerivacao"/>)
    /// é o contrato que faz a recusa: o VO valida que todo <c>contribui</c> pertence ao domínio, e
    /// devolve o mesmo erro nomeado que o decodificador do envelope aplica na reidratação — publish e
    /// decode barram o mesmo código desconhecido.
    /// </remarks>
    private DomainError? PendenciaDoDominioDeContribuicao()
    {
        IReadOnlyCollection<string> modalidadesOfertadas =
            [.. _distribuicaoVagas.SelectMany(static d => d.Modalidades).Select(static m => m.Codigo).Distinct(StringComparer.Ordinal)];

        foreach (ConfiguracaoDerivacaoFato config in _regrasDerivacao)
        {
            if (!string.Equals(config.CodigoFato, RegrasDerivacaoModalidadeLei12711.CodigoFato, StringComparison.Ordinal))
            {
                continue;
            }

            Result<RegrasDerivacaoFato> regras = config.ParaRegrasDerivacao(modalidadesOfertadas);
            if (regras.IsFailure)
            {
                return regras.Error;
            }
        }

        return null;
    }

    /// <summary>
    /// A condição de uma regra de derivação tem de citar um fato que exista no processo — coletado
    /// ou derivado (§7.3). O motor de derivação resolve os derivados em ordem, então um derivado
    /// que dependa de outro é resolúvel; o que ele não sabe fazer é resolver um fato que o processo
    /// não configura. O grafo conjunto ignora de propósito uma referência ausente (projeta só o
    /// que existe), então é esta recusa que prova a completude do vocabulário citado, antes que a
    /// aciclicidade e a ordem topológica sejam congeladas (RN08).
    /// </summary>
    /// <remarks>
    /// A pré-condição de campo já é barrada na definição (<see cref="ValidarGrafoDeFatos"/>): ela
    /// só cita coletado, porque o resolvedor de runtime não aciona o motor de derivação. O gatilho
    /// de exigência cita pelo mesmo vocabulário e caberá aqui quando passar a exigir os seus fatos.
    /// </remarks>
    private DomainError? PendenciaDeFatosCitados()
    {
        HashSet<string> universo = new(StringComparer.Ordinal);
        foreach (FatoColetado fato in _fatosColetados)
        {
            universo.Add(fato.FatoCodigo);
        }

        foreach (ConfiguracaoDerivacaoFato derivacao in _regrasDerivacao)
        {
            universo.Add(derivacao.CodigoFato);
        }

        foreach (ConfiguracaoDerivacaoFato derivacao in _regrasDerivacao)
        {
            foreach (RegraDerivacaoConfigurada regra in derivacao.Regras)
            {
                foreach (CondicaoRegraDerivacao condicao in regra.Condicoes)
                {
                    if (!universo.Contains(condicao.Fato))
                    {
                        return new DomainError(
                            FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado,
                            $"A regra de derivação do fato '{derivacao.CodigoFato}' cita '{condicao.Fato}', "
                            + "que este processo não coleta nem deriva.");
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Um fato categórico coletável cujo domínio é derivado da oferta do próprio processo
    /// (<c>CONDICAO_ATENDIMENTO</c>, <c>TIPO_DEFICIENCIA</c>) não pode ser publicado sem pelo
    /// menos um valor ofertado — obrigatório ou opcional, a pessoa candidata precisa de alguma
    /// opção para responder. Sem este gate, <c>DefinirFatosColetadosCommandHandler</c> aceita o
    /// vínculo (rascunho aceita configuração incompleta, de propósito) e
    /// <c>ResolvedorValoresSelecionaveisCongelados</c> congela a lista vazia — o formulário de
    /// inscrição publicado recebe um seletor sem nenhuma opção.
    /// </summary>
    private DomainError? PendenciaDeFatoColetadoSemValoresOfertados()
    {
        foreach (FatoColetado fato in _fatosColetados)
        {
            if (fato.TipoRenderizacao is not (TipoRenderizacao.SelecaoUnica or TipoRenderizacao.SelecaoMultipla))
            {
                continue;
            }

            bool ofertaVazia = fato.FatoCodigo switch
            {
                "CONDICAO_ATENDIMENTO" => (OfertaAtendimento?.Condicoes.Count ?? 0) == 0,
                "TIPO_DEFICIENCIA" => (OfertaAtendimento?.TiposDeficiencia.Count ?? 0) == 0,
                _ => false,
            };

            if (ofertaVazia)
            {
                return new DomainError(
                    "ProcessoSeletivo.FatoColetadoSemValoresOfertados",
                    $"O fato '{fato.FatoCodigo}' é coletável, mas a oferta do processo não declara nenhum valor para ele.");
            }
        }

        return null;
    }

    /// <summary>
    /// O grafo de dependência conjunto (campos, fatos, exigências e as quatro arestas, §6) tem
    /// de ser um DAG para ser congelável: a ordem topológica total que o snapshot congela (RN08)
    /// não existe se houver ciclo. A construção do grafo (<see cref="ConstruirGrafoDependencia"/>)
    /// já detecta o ciclo — aqui ela vira gate de publicação, antes de canonicalizar.
    /// </summary>
    /// <remarks>
    /// O grafo ignora deliberadamente uma referência a fato ausente (ele projeta o que existe),
    /// então este gate <b>não</b> prova sozinho que todo fato citado existe — essa é uma recusa
    /// à parte (§7.3). Aqui só se garante a aciclicidade.
    /// </remarks>
    private DomainError? PendenciaDoGrafoConjunto()
    {
        Result<GrafoDependenciaConjunta> grafo = ConstruirGrafoDependencia();
        return grafo.IsFailure ? grafo.Error : null;
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

            if (ConsequenciaRemoveVantagemSemVantagemViva(consequencia))
            {
                return new DomainError(
                    "DocumentoExigido.RemoveVantagemSemVantagemViva",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva (ex.: bônus regional) para remover.");
            }

            if (ModalidadeIncoerenteComConsequencia(consequencia, ModalidadesAlcancadasPor(exigencia)) is { } modalidadeIncoerente)
            {
                return new DomainError(
                    "DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' declara consequência '{consequencia}', incoerente com a ação de indeferimento '{modalidadeIncoerente.AcaoQuandoIndeferido}' configurada para a modalidade '{modalidadeIncoerente.Codigo}'.");
            }
        }

        // Story #920: os mesmos dois gates, estendidos ao grupo OU/N-de com consequência
        // própria (exigência de 1ª classe) — o alcance de modalidade é a união (OR) das
        // folhas descendentes (NoExigencia.PodeAlcancarModalidade).
        foreach (NoExigencia grupo in _nosExigencia.Where(static no => no.Tipo == TipoNo.GrupoOu && no.Consequencia is not null))
        {
            string consequenciaDoGrupo = grupo.Consequencia!;

            if (ConsequenciaRemoveVantagemSemVantagemViva(consequenciaDoGrupo))
            {
                return new DomainError(
                    "NoExigencia.RemoveVantagemSemVantagemViva",
                    $"O grupo '{grupo.Id}' declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva (ex.: bônus regional) para remover.");
            }

            if (ModalidadeIncoerenteComConsequencia(consequenciaDoGrupo, ModalidadesAlcancadasPor(grupo)) is { } modalidadeIncoerenteDoGrupo)
            {
                return new DomainError(
                    "NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga",
                    $"O grupo '{grupo.Id}' declara consequência '{consequenciaDoGrupo}', incoerente com a ação de indeferimento '{modalidadeIncoerenteDoGrupo.AcaoQuandoIndeferido}' configurada para a modalidade '{modalidadeIncoerenteDoGrupo.Codigo}'.");
            }
        }

        return null;
    }

    /// <summary>A consequência é REMOVE_VANTAGEM sem nenhuma vantagem viva no processo (bônus regional, RN05).</summary>
    private bool ConsequenciaRemoveVantagemSemVantagemViva(string? consequencia) =>
        consequencia == "REMOVE_VANTAGEM" && BonusRegional is null;

    /// <summary>
    /// A primeira modalidade alcançada cuja <c>AcaoQuandoIndeferido</c> declarada é incoerente com a
    /// consequência — ou <see langword="null"/> quando todas as que declaram ação concordam. Devolve a
    /// ENTIDADE (não um bool) porque tanto o gate (mensagem nomeando oferta/modalidade) quanto o
    /// checklist (presença) derivam do mesmo cálculo, sem recomputar a busca duas vezes.
    /// </summary>
    private static ModalidadeSelecionada? ModalidadeIncoerenteComConsequencia(
        string consequencia, IEnumerable<ModalidadeSelecionada> modalidadesAlcancadas)
    {
        string consequenciaComoAcaoDaVaga = NormalizarConsequenciaParaAcaoDaVaga(consequencia);
        return modalidadesAlcancadas.FirstOrDefault(modalidade => modalidade.AcaoQuandoIndeferido is { } acao
            && !string.Equals(acao, consequenciaComoAcaoDaVaga, StringComparison.Ordinal));
    }

    /// <summary>Existe <see cref="DocumentoExigido"/> (folha) com REMOVE_VANTAGEM sem vantagem viva.</summary>
    private bool ExisteExigenciaRemoveVantagemSemVantagemViva() =>
        _documentosExigidos.Any(e => ConsequenciaRemoveVantagemSemVantagemViva(e.ConsequenciaIndeferimento));

    /// <summary>Existe <see cref="DocumentoExigido"/> (folha) com consequência incoerente com a ação de alguma modalidade alcançada.</summary>
    private bool ExisteExigenciaConsequenciaIncoerenteComAcaoDaVaga() =>
        _documentosExigidos.Any(e => e.ConsequenciaIndeferimento is { } c
            && ModalidadeIncoerenteComConsequencia(c, ModalidadesAlcancadasPor(e)) is not null);

    /// <summary>Existe grupo OU/N-de com consequência própria REMOVE_VANTAGEM sem vantagem viva (Story #920).</summary>
    private bool ExisteGrupoRemoveVantagemSemVantagemViva() =>
        _nosExigencia.Any(no => no.Tipo == TipoNo.GrupoOu && ConsequenciaRemoveVantagemSemVantagemViva(no.Consequencia));

    /// <summary>Existe grupo OU/N-de com consequência própria incoerente com a ação de alguma modalidade alcançada (Story #920).</summary>
    private bool ExisteGrupoConsequenciaIncoerenteComAcaoDaVaga() =>
        _nosExigencia.Any(no => no.Tipo == TipoNo.GrupoOu && no.Consequencia is { } c
            && ModalidadeIncoerenteComConsequencia(c, ModalidadesAlcancadasPor(no)) is not null);

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
    /// Modalidades ofertadas que o gatilho da exigência PODE alcançar — verificação
    /// estrutural (<see cref="DocumentoExigido.PodeAlcancarModalidade"/>), não a avaliação
    /// factual de <see cref="DocumentoExigido.AplicavelPara"/> que o gate de conformidade
    /// legal usa (<see cref="Services.AvaliadorConformidadeLegal"/>) — aqui não há
    /// candidato real, só a pergunta "existe combinação de fatos em que este documento se
    /// torna exigido para alguém nesta modalidade?" (Story #554, PR #903, achado de
    /// revisão P2: <c>AplicavelPara</c> trataria qualquer gatilho não-modal, ex. só
    /// <c>FAIXA_ETARIA</c>, como nunca alcançando nenhuma modalidade).
    /// </summary>
    private IEnumerable<ModalidadeSelecionada> ModalidadesAlcancadasPor(DocumentoExigido exigencia) =>
        _distribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(modalidade => exigencia.PodeAlcancarModalidade(modalidade.Codigo));

    /// <summary>Mesma checagem estrutural acima, para um nó de grupo (Story #920) — <see cref="NoExigencia.PodeAlcancarModalidade"/> é a união (OR) das folhas descendentes.</summary>
    private IEnumerable<ModalidadeSelecionada> ModalidadesAlcancadasPor(NoExigencia no) =>
        _distribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(modalidade => no.PodeAlcancarModalidade(modalidade.Codigo));

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
        if (!ExisteGatilhoPorFaixaEtaria())
        {
            return null;
        }

        if (ReferenciaTemporalFatosAusenteQuandoExigida())
        {
            return new DomainError(
                "ProcessoSeletivo.ReferenciaTemporalFatosAusente",
                "Existe gatilho por FAIXA_ETARIA, mas nenhuma referência temporal de fatos foi configurada — a publicação não pode resolver a idade do candidato sem fallback silencioso (ADR-0111).");
        }

        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos!;

        if (referencia.Tipo is ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase)
        {
            if (ReferenciaTemporalFatosFaseNaoPertenceAoCronograma())
            {
                return new DomainError(
                    "ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente",
                    "A fase âncora da referência temporal de fatos não pertence (mais) ao cronograma deste processo.");
            }

            if (ReferenciaTemporalFatosExtremoDaFaseAusente())
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
            // IdadeMaximaEmissao, PR #900) — a pergunta certa é existencial (Any), não posicional.
            if (ReferenciaTemporalFatosFimInscricaoIndisponivel())
            {
                return new DomainError(
                    "ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel",
                    "FIM_INSCRICAO exige uma fase que colete inscrição com Fim definido — sem fallback silencioso.");
            }
        }

        // DATA_ESPECIFICA: ReferenciaTemporalFatos.Criar já garante Data presente — nada a checar aqui.
        return null;
    }

    /// <summary>Existe gatilho por FAIXA_ETARIA em algum <see cref="DocumentoExigido"/> — sem ele, a referência temporal é opcional.</summary>
    private bool ExisteGatilhoPorFaixaEtaria() =>
        _documentosExigidos.SelectMany(static d => d.Condicoes)
            .Any(static c => string.Equals(c.Fato, "FAIXA_ETARIA", StringComparison.Ordinal));

    /// <summary>A fase âncora de InicioFase/FimFase — <see langword="null"/> quando o tipo não usa fase ou ela não pertence (mais) ao cronograma.</summary>
    private FaseCronograma? FaseAncoraDaReferenciaTemporal() =>
        ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } referencia
            ? _cronogramaFases.FirstOrDefault(f => f.Id == referencia.FaseId)
            : null;

    private bool ReferenciaTemporalFatosAusenteQuandoExigida() =>
        ExisteGatilhoPorFaixaEtaria() && ReferenciaTemporalFatos is null;

    private bool ReferenciaTemporalFatosFaseNaoPertenceAoCronograma() =>
        ExisteGatilhoPorFaixaEtaria()
        && ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase }
        && FaseAncoraDaReferenciaTemporal() is null;

    private bool ReferenciaTemporalFatosExtremoDaFaseAusente() =>
        ExisteGatilhoPorFaixaEtaria()
        && ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } referencia
        && FaseAncoraDaReferenciaTemporal() is { } fase
        && (referencia.Tipo == ReferenciaTipo.InicioFase ? fase.Inicio : fase.Fim) is null;

    private bool ReferenciaTemporalFatosFimInscricaoIndisponivel() =>
        ExisteGatilhoPorFaixaEtaria()
        && ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.FimInscricao }
        && !_cronogramaFases.Any(static f => f.ColetaInscricao && f.Fim is not null);

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
    /// <summary>
    /// A fase do cronograma que ancora o período de inscrição do Edital (issue #1350) — a de
    /// menor <see cref="FaseCronograma.Ordem"/> entre as que coletam inscrição, ou
    /// <see langword="null"/> quando o certame não coleta inscrição pelo sistema.
    /// </summary>
    /// <remarks>
    /// Nada impede mais de uma fase com <see cref="FaseCronograma.ColetaInscricao"/>, e a coleção
    /// preserva a ordem de ENTRADA, não a de <c>Ordem</c> — mesma armadilha que
    /// <see cref="ResolverDataReferenciaFatos"/> documenta: o envelope escreve as fases ordenadas
    /// por <c>Ordem</c>, então escolher posicionalmente faria a reidratação eleger outra fase e
    /// quebrar o round-trip. <c>Ordem</c> é única por processo, o que torna a escolha determinística.
    /// <para>
    /// Havendo duas janelas de coleta, o período publicado é o da primeira. É consequência aceita:
    /// a segunda janela (tipicamente remanejamento) não estende o período declarado no Edital.
    /// </para>
    /// </remarks>
    public FaseCronograma? FaseQueAncoraOPeriodoDeInscricao() => _cronogramaFases
        .Where(static f => f.ColetaInscricao)
        .OrderBy(static f => f.Ordem)
        .FirstOrDefault();

    /// <param name="fusoHorario">
    /// Zona em que a âncora vira dia civil. Vem da canonicalização — na publicação é o fuso
    /// institucional corrente, e ao provar uma versão já publicada é o que ela congelou. Deixar o
    /// método escolher a zona sozinho faria o envelope declarar um fuso no bloco de localidade e
    /// calcular a data por outro, congelando o dia civil errado numa versão imutável.
    /// </param>
    public DateOnly? ResolverDataReferenciaFatos(TimeZoneInfo fusoHorario)
    {
        ArgumentNullException.ThrowIfNull(fusoHorario);

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
            // Achado de revisão (Story #554, PR #903): _cronogramaFases preserva a ORDEM DE
            // ENTRADA de DefinirCronogramaFases, mas o envelope escreve as fases ordenadas
            // por Ordem (canonicalização determinística) — havendo mais de uma fase de
            // coleta com Fim, a fase publicada dependeria da ordem em que o caller as
            // passou, e a reidratação (sempre em ordem de Ordem) poderia escolher outra,
            // quebrando o round-trip (SombraParaVerificacao). OrderBy(Ordem) torna a escolha
            // determinística e independente de como a coleção foi populada.
            ReferenciaTipo.FimInscricao => _cronogramaFases
                .Where(static f => f.ColetaInscricao && f.Fim is not null)
                .OrderBy(static f => f.Ordem)
                .FirstOrDefault()?.Fim,
            _ => throw new InvalidOperationException($"Tipo de ReferenciaTemporalFatos não reconhecido: {referencia.Tipo}."),
        };

        if (extremo is not { } instante)
        {
            throw new InvalidOperationException(
                "dataReferenciaFatos não resolvível a partir do cronograma — PendenciaDaReferenciaTemporalFatos deveria ter recusado a transição antes deste ponto.");
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, fusoHorario).DateTime);
    }

    /// <summary>
    /// CA-03 (Story #554, issue #892): um gatilho DNF sobre um
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
        TimeProvider clock,
        ContextoDeContagemDePrazos contexto)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(configuracaoCongeladaCanonica);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(contexto);

        if (Status != StatusProcesso.Rascunho)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível publicar um processo em rascunho — status atual: {Status}."));
        }

        // Localidade (UNI-REQ-0111): a versão publicada congela o município cujo calendário rege a
        // contagem dos prazos, e sem ele a janela de recurso não se recalcula. Desde que a
        // localidade é exigida na criação, chegar aqui sem ela não é estado alcançável pelo fluxo
        // público — a recusa fica como invariante da raiz, defendendo contra caminho de escrita que
        // a contorne, e porque o requisito exige a recusa nomeada nas três transições que geram
        // versão. Publicar cobre a inicial; SucederVersao cobre a retificação e o fechamento.
        if (PendenciaDaLocalidade() is { } pendenciaLocalidade)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaLocalidade);
        }

        // Convenção de contagem (UNI-REQ-0112): exigida só quando alguma contagem do
        // certame distingue dia útil. Escolher em silêncio produziria versão imutável cujo
        // prazo o sistema teria decidido no lugar do edital.
        if (PendenciaDoAlgoritmoDeContagem() is { } pendenciaAlgoritmo)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaAlgoritmo);
        }

        if (PendenciaDoCalendarioVigente(contexto) is { } pendenciaCalendario)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCalendario);
        }

        if (PendenciaDeConformidade() is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        if (PendenciaDoCronograma() is { } pendenciaCronograma)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCronograma);
        }

        if (PendenciaDaCascata() is { } pendenciaCascata)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCascata);
        }

        if (PendenciaPreCanonicalizacao() is { } pendenciaPreCanonicalizacao)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaPreCanonicalizacao);
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
        TimeProvider clock,
        ContextoDeContagemDePrazos contexto)
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
            hashDocumento, atorUsuarioSub, motivo, clock, contexto);
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
        TimeProvider clock,
        ContextoDeContagemDePrazos contexto)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (PendenciaDaSessaoEditorial(precondicao) is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        Result<VersaoConfiguracao> versao = SucederVersao(
            dados, versaoAtual, configuracaoCongeladaCanonica, schemaVersion, algoritmoHash,
            hashDocumento, atorUsuarioSub, Rascunho!.Motivo, clock, contexto);
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
        TimeProvider clock,
        ContextoDeContagemDePrazos contexto)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(contexto);
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
        // Localidade (UNI-REQ-0111): a versão publicada congela o município cujo calendário rege a
        // contagem dos prazos, e sem ele a janela de recurso não se recalcula. Desde que a
        // localidade é exigida na criação, chegar aqui sem ela não é estado alcançável pelo fluxo
        // público — a recusa fica como invariante da raiz, defendendo contra caminho de escrita que
        // a contorne, e porque o requisito exige a recusa nomeada nas três transições que geram
        // versão. Publicar cobre a inicial; SucederVersao cobre a retificação e o fechamento.
        if (PendenciaDaLocalidade() is { } pendenciaLocalidade)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaLocalidade);
        }

        // Convenção de contagem (UNI-REQ-0112): exigida só quando alguma contagem do
        // certame distingue dia útil. Escolher em silêncio produziria versão imutável cujo
        // prazo o sistema teria decidido no lugar do edital.
        if (PendenciaDoAlgoritmoDeContagem() is { } pendenciaAlgoritmo)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaAlgoritmo);
        }

        if (PendenciaDoCalendarioVigente(contexto) is { } pendenciaCalendario)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCalendario);
        }

        if (PendenciaDeConformidade() is { } pendencia)
        {
            return Result<VersaoConfiguracao>.Failure(pendencia);
        }

        if (PendenciaDoCronograma() is { } pendenciaCronograma)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCronograma);
        }

        if (PendenciaDaCascata() is { } pendenciaCascata)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaCascata);
        }

        if (PendenciaPreCanonicalizacao() is { } pendenciaPreCanonicalizacao)
        {
            return Result<VersaoConfiguracao>.Failure(pendenciaPreCanonicalizacao);
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
    /// <param name="grafo">As dimensões reconstruídas pelo codec do envelope (ADR-0110 D1).</param>
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
        TipoProcesso = TipoProcesso,
        Status = Status,
        OrigemCandidatos = OrigemCandidatos,
        UnidadeAdministradoraOrigemId = UnidadeAdministradoraOrigemId,
        UnidadeAdministradora = UnidadeAdministradora,
        // A localidade nasce igual à do processo e é sobrescrita pela congelada quando o grafo a
        // traz. Copiá-la aqui não é redundância: a sombra é recanonicalizada, e o bloco de
        // localidade a lê — sem isso, restaurar um envelope anterior a esta fatia projetaria
        // sobre uma raiz sem município.
        Localidade = Localidade,
        // Mesma razão da localidade: a sombra é recanonicalizada, e o bloco da convenção de
        // contagem lê da raiz projetada.
        AlgoritmoContagemPrazo = AlgoritmoContagemPrazo,
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

    private static DomainError? ValidarGrafo(GrafoConfiguracao grafo)
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

        // A checagem ENEM×eliminação NÃO se repete aqui: grafo.Classificacao chega
        // já construído por ConfiguracaoClassificacao.Criar (via
        // EnvelopeCodecV11.LerClassificacao) — se o envelope violasse a invariante,
        // a decodificação já teria falhado antes de ValidarGrafo ser chamado.
        foreach (RegraEliminacao regra in grafo.Classificacao.RegrasEliminacao)
        {
            // INV-B4 — mesma proteção do INV-B6, para a eliminação por nota mínima.
            if (regra.Args is ArgsElimNotaMinimaEtapa notaMinima && !idsEtapas.Contains(notaMinima.EtapaRef))
            {
                return new DomainError(
                    "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
                    $"A regra de eliminação referencia a etapa {notaMinima.EtapaRef}, que não existe na configuração restaurada (INV-B4).");
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

        return ValidarNosExigencia(grafo);
    }

    /// <summary>
    /// Story #923 — as checagens estruturais da árvore de satisfação restaurada,
    /// equivalentes ao que <c>NoExigencia.CriarGrupo</c> já garante na ESCRITA (grupo não
    /// vazio, ciclo, fase comum, cardinalidade por tipo) mais o que só a RAIZ consegue
    /// provar (INV-B6-símile: <c>DocumentoExigidoId</c> de folha existe em
    /// <c>grafo.DocumentosExigidos</c>) e o que só o BANCO garante na escrita normal (Id
    /// único, <c>Ordem</c> única entre raízes e entre irmãos — <c>ux_nos_exigencia_raiz_ordem</c>/
    /// <c>ux_nos_exigencia_irmaos_ordem</c>, <c>NoExigenciaConfiguration</c>). Sem esta
    /// última checagem, um envelope com duas raízes de mesma <c>Ordem</c> passaria pela
    /// decodificação e só falharia no <c>SaveChanges</c>, como <c>23505</c> (unique
    /// violation) — 500 não tratado no meio de uma restauração, em vez de recusa nomeada.
    /// </summary>
    private static DomainError? ValidarNosExigencia(GrafoConfiguracao grafo)
    {
        List<Guid> idsNos = [.. grafo.NosExigencia.Select(n => n.Id)];
        if (idsNos.Distinct().Count() != idsNos.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.IdNoExigenciaDuplicado",
                "O mesmo Id de nó da árvore de satisfação não pode aparecer mais de uma vez na configuração restaurada.");
        }

        List<int> ordensRaizes = [.. grafo.NosExigencia.Where(static n => n.NoPaiId is null).Select(static n => n.Ordem)];
        if (ordensRaizes.Distinct().Count() != ordensRaizes.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemRaizNoExigenciaDuplicada",
                "Cada raiz da árvore de satisfação deve ter uma ordem única dentro do processo.");
        }

        bool irmaosComOrdemDuplicada = grafo.NosExigencia
            .Where(static n => n.NoPaiId is not null)
            .GroupBy(static n => n.NoPaiId)
            .Any(static grupo => grupo.Select(static n => n.Ordem).Distinct().Count() != grupo.Count());
        if (irmaosComOrdemDuplicada)
        {
            return new DomainError(
                "ProcessoSeletivo.OrdemIrmaoNoExigenciaDuplicada",
                "Cada filho de um mesmo nó pai deve ter uma ordem única entre os irmãos.");
        }

        // NoExigencia.Reidratar não revalida nada do que NoExigencia.CriarFolha/CriarGrupo
        // garantem na ESCRITA (cardinalidade de folha e de grupo, janela de calendário,
        // consequência, base legal, repetição aninhada). Em vez de duplicar esses
        // invariantes campo a campo, cada raiz é RECONSTRUÍDA aqui pelos MESMOS factories
        // que o cadastro usa — o resultado é descartado (só prova que a árvore SERIA aceita
        // pelo cadastro; a árvore com identidade preservada continua sendo a decodificada
        // por Reidratar, ADR-0110 D2), mas herda de uma vez só todo invariante de
        // construção presente e futuro, sem nunca mais divergir deles.
        if (grafo.NosExigencia.Any(static n => n.Tipo == TipoNo.Folha && n.Filhos.Count > 0))
        {
            return new DomainError(
                "ProcessoSeletivo.NoExigenciaFolhaComFilhos",
                "Uma folha da árvore de satisfação restaurada não pode ter filhos — CriarFolha nunca aceita filhos.");
        }

        foreach (NoExigencia raiz in grafo.NosExigencia.Where(static n => n.NoPaiId is null))
        {
            Result<NoExigencia> reconstrucao = ReconstruirNoParaValidarInvariantes(raiz);
            if (reconstrucao.IsFailure)
            {
                return reconstrucao.Error;
            }
        }

        // Folha ↔ DocumentoExigido: EXATAMENTE 1:1 — nem "a mais" (referenciando um
        // DocumentoExigido que não existe no grafo) nem "a menos" (duas folhas para o mesmo
        // DocumentoExigido, que só falharia depois no SaveChanges contra
        // ux_nos_exigencia_documento_exigido_id).
        HashSet<Guid> idsDocumentosExigidos = [.. grafo.DocumentosExigidos.Select(static d => d.Id)];
        List<Guid> documentoIdsReferenciados = [.. grafo.NosExigencia
            .Where(static n => n.Tipo == TipoNo.Folha)
            .Select(static n => n.DocumentoExigidoId!.Value)];

        if (documentoIdsReferenciados.Any(id => !idsDocumentosExigidos.Contains(id)))
        {
            return new DomainError(
                "ProcessoSeletivo.NoExigenciaFolhaSemDocumentoExigido",
                "Uma folha da árvore de satisfação restaurada referencia um DocumentoExigido que não existe na configuração restaurada.");
        }

        if (documentoIdsReferenciados.Distinct().Count() != documentoIdsReferenciados.Count)
        {
            return new DomainError(
                "ProcessoSeletivo.NoExigenciaDocumentoExigidoDuplicado",
                "Duas folhas da árvore de satisfação restaurada não podem referenciar o mesmo DocumentoExigido.");
        }

        return null;
    }

    /// <summary>
    /// Reconstrói recursivamente um nó (e a subárvore dele) via
    /// <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/> — ver a nota
    /// em <see cref="ValidarNosExigencia"/>. Bottom-up: os filhos são reconstruídos (e
    /// validados) antes do pai, porque <c>CriarGrupo</c> precisa deles para checar grupo
    /// vazio, mesma fase e repetição aninhada.
    /// </summary>
    private static Result<NoExigencia> ReconstruirNoParaValidarInvariantes(NoExigencia no)
    {
        Result<NoExigencia> resultado;

        if (no.Tipo == TipoNo.Folha)
        {
            resultado = NoExigencia.CriarFolha(
                no.DocumentoExigido!, no.Ordem, no.QuantidadeMinima, no.ChaveDistincao,
                no.DataReferencia, no.OcorrenciasEsperadas, no.RepetePorEntidade);
        }
        else if (no.Tipo is TipoNo.GrupoE or TipoNo.GrupoOu)
        {
            List<NoExigencia> filhosReconstruidos = new(no.Filhos.Count);
            foreach (NoExigencia filho in no.Filhos)
            {
                Result<NoExigencia> filhoResultado = ReconstruirNoParaValidarInvariantes(filho);
                if (filhoResultado.IsFailure)
                {
                    return filhoResultado;
                }

                filhosReconstruidos.Add(filhoResultado.Value!);
            }

            List<NoExigenciaBaseLegal> basesLegaisReconstruidas = new(no.BasesLegais.Count);
            foreach (NoExigenciaBaseLegal baseLegal in no.BasesLegais)
            {
                Result<NoExigenciaBaseLegal> baseLegalResultado = NoExigenciaBaseLegal.Criar(
                    baseLegal.Referencia, baseLegal.Abrangencia, baseLegal.Status, baseLegal.Observacao);
                if (baseLegalResultado.IsFailure)
                {
                    return Result<NoExigencia>.Failure(baseLegalResultado.Error!);
                }

                basesLegaisReconstruidas.Add(baseLegalResultado.Value!);
            }

            resultado = NoExigencia.CriarGrupo(
                no.Tipo, no.Ordem, no.QuantidadeMinima, no.Consequencia, basesLegaisReconstruidas,
                filhosReconstruidos, no.RepetePorEntidade);
        }
        else
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaTipoInvalido",
                $"O nó {no.Id} da árvore de satisfação restaurada tem um tipo desconhecido."));
        }

        return resultado.IsFailure ? resultado : ValidarCanonicidade(no, resultado.Value!);
    }

    /// <summary>
    /// <see cref="NoExigencia.CriarFolha"/>/<see cref="NoExigencia.CriarGrupo"/> não só
    /// VALIDAM — alguns campos são NORMALIZADOS silenciosamente (<c>quantidadeMinima</c>
    /// nula vira <see cref="NoExigencia.QuantidadeMinimaPadrao"/>; consequência em branco
    /// vira <see langword="null"/>). A reconstrução por si só SUCEDE mesmo quando o nó
    /// DECODIFICADO tinha o valor não-canônico — e é o nó DECODIFICADO, não o reconstruído,
    /// que é aplicado ao agregado (ADR-0110 D2). Sem esta comparação, uma folha ou grupo
    /// OU/N-de com <c>quantidadeMinima: null</c> passaria por aqui e só falharia depois, no
    /// <c>SaveChanges</c>, contra <c>ck_nos_exigencia_tipo_campos_coerentes</c>.
    /// </summary>
    private static Result<NoExigencia> ValidarCanonicidade(NoExigencia decodificado, NoExigencia reconstruido)
    {
        if (reconstruido.QuantidadeMinima != decodificado.QuantidadeMinima)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaQuantidadeMinimaAusente",
                $"O nó {decodificado.Id} da árvore de satisfação restaurada não declara quantidadeMinima — um encoder real nunca a omite em folha ou grupo OU/N-de."));
        }

        if (reconstruido.Consequencia != decodificado.Consequencia)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaConsequenciaNaoCanonica",
                $"O nó {decodificado.Id} da árvore de satisfação restaurada declara uma consequência que não está na forma canônica."));
        }

        // CriarFolha e CriarGrupo não compartilham todos os parâmetros — cada um aceita só
        // os campos do seu próprio tipo (chaveDistincao/dataReferencia/ocorrenciasEsperadas
        // são exclusivos de folha; basesLegais própria é exclusiva de grupo). A reconstrução
        // de um nó com o tipo errado desses campos simplesmente os IGNORA (nem chegam a ser
        // parâmetro da factory) em vez de recusar, e a comparação acima não pega isso. Um nó
        // decodificado carregando o campo do outro tipo violaria
        // ck_nos_exigencia_tipo_campos_coerentes no SaveChanges (folha/grupo) ou persistiria
        // uma base legal em folha que a API de escrita normal nunca produz.
        if (decodificado.Tipo == TipoNo.Folha)
        {
            if (decodificado.BasesLegais.Count > 0)
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "ProcessoSeletivo.NoExigenciaFolhaComBaseLegal",
                    $"O nó {decodificado.Id} da árvore de satisfação restaurada é uma folha, mas carrega base legal — campo exclusivo de grupo OU/N-de."));
            }
        }
        else if (decodificado.ChaveDistincao is not null
            || decodificado.DataReferencia is not null
            || decodificado.OcorrenciasEsperadas is not null)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "ProcessoSeletivo.NoExigenciaGrupoComCampoDeFolha",
                $"O nó {decodificado.Id} da árvore de satisfação restaurada é um grupo, mas carrega chaveDistincao, dataReferencia ou ocorrenciasEsperadas — campos exclusivos de folha."));
        }

        return Result<NoExigencia>.Success(reconstruido);
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
                    congelada.TipoEtapa,
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

        grafo.CascataRemanejamento?.VincularProcesso(Id);
        Cascata = grafo.CascataRemanejamento;

        _criteriosDesempate.Clear();
        foreach (CriterioDesempate criterio in grafo.CriteriosDesempate)
        {
            criterio.VincularProcesso(Id);
            _criteriosDesempate.Add(criterio);
        }

        grafo.Classificacao.VincularProcesso(Id);
        Classificacao = grafo.Classificacao;

        // Formulário de inscrição (Story #559): escalares simples da própria raiz, sem
        // reconciliação — reatribuídos direto, mesmo padrão de BonusRegional/Cascata acima. Sem
        // isso, editar o título durante uma sessão de retificação e depois descartar deixaria o
        // valor editado na configuração viva, driblando RN08 exatamente pelos dois campos que
        // esta reposição cobre.
        FormularioTitulo = grafo.FormularioTitulo;
        FormularioTermoAceiteTexto = grafo.FormularioTermoAceiteTexto;

        // Divulgação pública (UNI-REQ-0050, issue #563): mesmo padrão de BonusRegional/Cascata
        // acima — reatribuição direta (toggle por presença). Sem esta reposição, editar a
        // divulgação durante uma sessão de retificação e depois descartar deixaria o valor
        // editado na configuração viva, o mesmo defeito que a reposição de BonusRegional evita.
        grafo.ConfiguracaoDivulgacao?.VincularProcesso(Id);
        ConfiguracaoDivulgacao = grafo.ConfiguracaoDivulgacao;

        // Taxa de inscrição e isenção (issue #1112): mesmo padrão de reposição de
        // BonusRegional/Divulgacao acima. Sem esta reposição, editar a taxa durante uma sessão
        // de retificação e depois descartar deixaria o valor editado na configuração viva.
        grafo.ConfiguracaoTaxaInscricao?.VincularProcesso(Id);
        ConfiguracaoTaxaInscricao = grafo.ConfiguracaoTaxaInscricao;

        // Localidade (UNI-REQ-0111): repõe a que estava congelada, senão editar o município sob
        // sessão editorial e descartar deixaria a nova governando a contagem dos prazos. Só
        // sobrescreve quando o grafo a traz — grafos de teste anteriores a esta fatia não têm, e
        // apagar a localidade viva deixaria a raiz num estado que o domínio não admite.
        if (grafo.Localidade is { } localidadeCongelada)
        {
            Localidade = localidadeCongelada;
        }

        // Convenção de contagem (UNI-REQ-0112): reposição INCONDICIONAL, ao contrário da
        // localidade acima. Ali, null significa grafo antigo que não a carrega, e apagar
        // deixaria a raiz num estado que o domínio não admite. Aqui, null é o que a versão
        // de fato tinha quando nenhuma contagem sua distinguia dia útil — repor só quando
        // presente faria a declaração feita durante a sessão sobreviver ao descarte, que é
        // precisamente o que o descarte existe para desfazer.
        AlgoritmoContagemPrazo = grafo.AlgoritmoContagemPrazo;

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

        // Achado de revisão (Story #554, PR #903): a reconciliação acima troca o Id
        // CONGELADO de uma fase pelo Id da instância VIVA sempre que reusa uma fase
        // tracked na mesma Ordem. `documentosExigidos`/`referenciaTemporalFatos`
        // referenciam fases pelo Id CONGELADO (o que estava vigente quando o snapshot foi
        // produzido) — sem este mapa, restaurar um snapshot cuja sessão de rascunho trocou
        // a fase de uma Ordem deixaria essas referências apontando para um Id ausente de
        // CronogramaFases após a restauração.
        Dictionary<Guid, Guid> faseIdCongeladaParaViva = [];
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
                if (viva.Id != congelada.Id)
                {
                    faseIdCongeladaParaViva[congelada.Id] = viva.Id;
                }
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
        // instância TRACKED das demais coleções acima. `RemapearFase` corrige
        // `ExigidoNaFaseId` para o Id da fase VIVA quando a reconciliação acima trocou de
        // instância — sem efeito quando a fase reconciliada manteve o mesmo Id.
        Dictionary<Guid, DocumentoExigido> documentosExigidosTracked = _documentosExigidos.ToDictionary(d => d.Id);
        List<DocumentoExigido> documentosExigidos = [];
        foreach (DocumentoExigido congelada in grafo.DocumentosExigidos)
        {
            if (faseIdCongeladaParaViva.TryGetValue(congelada.ExigidoNaFaseId, out Guid faseVivaId))
            {
                congelada.RemapearFase(faseVivaId);
            }

            documentosExigidos.Add(documentosExigidosTracked.TryGetValue(congelada.Id, out DocumentoExigido? viva) ? viva : congelada);
        }

        _documentosExigidos.Clear();
        foreach (DocumentoExigido documento in documentosExigidos)
        {
            documento.VincularProcesso(Id);
            _documentosExigidos.Add(documento);
        }

        // Árvore de satisfação (Story #920, wrapper de árvore no envelope — Story #923):
        // reconciliação por Id, mesmo padrão de DocumentosExigidos acima. Um
        // NoExigencia.Id só sobrevive intacto entre publicações quando NADA da árvore foi
        // reeditado no meio — DefinirDocumentosExigidos substitui a árvore por INTEIRO a
        // cada chamada, mintando Guids novos, nunca atualiza um nó existente — então reusar
        // a instância TRACKED sempre que o Id bate é seguro: o conteúdo dela já É, por
        // construção, o mesmo do congelado (mesma garantia que sustenta a reconciliação
        // acima). `RemapearDocumentoExigido` corrige a navegação de folha para a instância
        // FINAL de `documentosExigidos` (viva ou congelada, o que a reconciliação acima já
        // escolheu) — sem isso, uma folha cuja exigência foi reconciliada para a instância
        // viva ficaria com `DocumentoExigido` apontando para o objeto congelado descartado.
        Dictionary<Guid, DocumentoExigido> documentosExigidosPorId = documentosExigidos.ToDictionary(d => d.Id);
        Dictionary<Guid, NoExigencia> nosExigenciaTracked = _nosExigencia.ToDictionary(n => n.Id);
        List<NoExigencia> nosExigencia = [];
        foreach (NoExigencia congelado in grafo.NosExigencia)
        {
            NoExigencia no = nosExigenciaTracked.TryGetValue(congelado.Id, out NoExigencia? vivo) ? vivo : congelado;
            if (no.Tipo == TipoNo.Folha
                && documentosExigidosPorId.TryGetValue(no.DocumentoExigidoId!.Value, out DocumentoExigido? documentoFinal))
            {
                no.RemapearDocumentoExigido(documentoFinal);
            }

            nosExigencia.Add(no);
        }

        _nosExigencia.Clear();
        foreach (NoExigencia raiz in nosExigencia.Where(static n => n.NoPaiId is null))
        {
            raiz.VincularProcesso(Id);
        }

        foreach (NoExigencia no in nosExigencia)
        {
            _nosExigencia.Add(no);
        }

        // Referência temporal de fatos (Story #554, PR #903, B-03): o envelope congela a
        // POLÍTICA crua (Tipo/Data/FaseId) ao lado da data já resolvida — mesmo padrão de
        // "insumo + output derivado" de distribuicao/vagas. Repor a política (não só a
        // data) é o que torna o round-trip reidratar→recanonicalizar não-tautológico:
        // ResolverDataReferenciaFatos() recalcula o mesmo output a partir do mesmo insumo
        // restaurado. Cada VersaoConfiguracao congela sua PRÓPRIA política — uma
        // retificação que muda a política antes de publicar não afeta o que já foi
        // congelado (B-03). O FaseId também passa pelo mesmo remapeamento de
        // `documentosExigidos` acima, pelo mesmo motivo (INICIO_FASE/FIM_FASE apontam para
        // um Id congelado que pode ter sido substituído pela fase viva).
        ReferenciaTemporalFatos = grafo.ReferenciaTemporalFatos is { FaseId: { } faseIdCongelada }
            referencia && faseIdCongeladaParaViva.TryGetValue(faseIdCongelada, out Guid faseVivaIdReferencia)
                ? referencia.ComFaseIdRemapeada(faseVivaIdReferencia)
                : grafo.ReferenciaTemporalFatos;

        // Fatos coletados e regras de derivação (Story #928, §7.4): repostos da configuração
        // congelada. A reconciliação é por chave natural (FatoCodigo / CodigoFato) reusando a
        // instância TRACKED quando o código bate — segura quando a coleção viva NÃO foi editada (o
        // conteúdo da viva JÁ É o do congelado, como na sombra de verificação, que nasce vazia, e no
        // caminho de restauração sem edição prévia). Sob edição sob retificação (Story #986), a viva
        // pode ter trocado ordens ou alterado pré-condições/regras: reusá-la NÃO restauraria o
        // congelado, e substituí-la na mesma transação colidiria no índice único de Ordem/código.
        // Por isso a orquestração do descarte chama LimparColetaEDerivacaoParaRestauracao() e FAZ UM
        // SaveChanges intermediário ANTES desta reposição: as coleções chegam aqui vazias, o ramo
        // TRACKED não é tomado, e as instâncias CONGELADAS entram como INSERT (Id novo da
        // reidratação — nenhuma identidade precisa sobreviver). A reposição fiel de graça, sem
        // reconciliação profunda dos filhos.
        Dictionary<string, FatoColetado> fatosTracked = _fatosColetados.ToDictionary(f => f.FatoCodigo, StringComparer.Ordinal);
        _fatosColetados.Clear();
        foreach (FatoColetado congelado in grafo.FatosColetados)
        {
            FatoColetado fato = fatosTracked.TryGetValue(congelado.FatoCodigo, out FatoColetado? vivo) ? vivo : congelado;
            fato.VincularProcessoSeletivo(Id);
            _fatosColetados.Add(fato);
        }

        Dictionary<string, ConfiguracaoDerivacaoFato> derivacoesTracked =
            _regrasDerivacao.ToDictionary(c => c.CodigoFato, StringComparer.Ordinal);
        _regrasDerivacao.Clear();
        foreach (ConfiguracaoDerivacaoFato congelada in grafo.RegrasDerivacao)
        {
            ConfiguracaoDerivacaoFato config =
                derivacoesTracked.TryGetValue(congelada.CodigoFato, out ConfiguracaoDerivacaoFato? viva) ? viva : congelada;
            config.VincularProcessoSeletivo(Id);
            _regrasDerivacao.Add(config);
        }
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
