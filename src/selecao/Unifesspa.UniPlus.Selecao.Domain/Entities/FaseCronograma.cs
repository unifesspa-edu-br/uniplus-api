namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Collections.Frozen;

using Enums;

using Services;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;

using ValueObjects;

/// <summary>
/// Uma fase do cronograma de um <see cref="ProcessoSeletivo"/>: o eixo <b>temporal</b> do certame
/// (janela, ordem, unidade responsável, o que a fase publica), distinto do eixo de <b>pontuação</b>
/// (<see cref="EtapaProcesso"/>). Snapshot-copy (ADR-0061) de uma <c>FaseCanonica</c> de Configuração,
/// sem FK para o cadastro vivo.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft delete): a configuração em rascunho é substituível
/// por inteiro (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>). O que a fase publica é declarado
/// em <see cref="Produtos"/>, e que o papel só caiba em ato que é resultado no catálogo é I/O, da
/// Application (ADR-0042). O ciclo recursal fecha dentro da própria fase: o preliminar de um tipo de ato
/// exige o definitivo do mesmo tipo nela.
/// </remarks>
public sealed class FaseCronograma : EntityBase, IParteDoProcesso, IPeriodoDaFase
{
    public const int TipoFaseNomeMaxLength = 200;

    public ChaveLocal Chave { get; private set; }

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Posição do certame na linha do tempo, única dentro do cronograma.</summary>
    public int Ordem { get; private set; }

    /// <summary>Se a fase acontece no mesmo momento da fase que a antecede na lista; a fase sem a marca acontece depois dela.</summary>
    public bool NoMesmoMomentoDaAnterior { get; private set; }

    /// <summary>Id (Guid v7) da <c>FaseCanonica</c> viva de origem, no momento do congelamento.</summary>
    public Guid FaseCanonicaOrigemId { get; private set; }

    /// <summary>Código canônico congelado (ex.: <c>"HOMOLOGACAO_INSCRICOES"</c>).</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string TipoFaseNome { get; private set; } = string.Empty;

    /// <summary>A unidade responsável, opcional no rascunho e exigida na publicação.</summary>
    public UnidadeResponsavelSnapshot? UnidadeResponsavel { get; private set; }

    /// <summary>Quando a data da fase é fixada (no edital ou na execução); decide a obrigatoriedade da janela.</summary>
    public OrigemDataFase OrigemData { get; private set; }

    /// <summary>Se a fase agrupa etapas pontuadas: bicondicional com a existência de etapa.</summary>
    public bool AgrupaEtapas { get; private set; }

    public bool PermiteComplementacao { get; private set; }

    /// <summary>Se a fase coleta inscrição; decide o piso mínimo quando <see cref="OrigemCandidatos.InscricaoPropria"/>.</summary>
    public bool ColetaInscricao { get; private set; }

    /// <summary>Se a fase abre a janela de pedido de isenção da taxa.</summary>
    public bool ColetaSolicitacaoIsencao { get; private set; }

    private static readonly FinalidadeFormulario[] FinalidadesNaOrdemDeApresentacao =
        [FinalidadeFormulario.Inscricao, FinalidadeFormulario.IsencaoTaxa, FinalidadeFormulario.Habilitacao];

    /// <summary>
    /// As finalidades cujo formulário se responde nesta fase; inscrição e isenção podem dividir a mesma fase.
    /// </summary>
    public IReadOnlyList<FinalidadeFormulario> FinalidadesQueAtende() =>
        [.. FinalidadesNaOrdemDeApresentacao.Where(Atende)];

    /// <summary>Se o formulário da finalidade se responde nesta fase.</summary>
    public bool Atende(FinalidadeFormulario finalidade) => finalidade switch
    {
        FinalidadeFormulario.Inscricao => ColetaInscricao,
        FinalidadeFormulario.IsencaoTaxa => ColetaSolicitacaoIsencao,
        FinalidadeFormulario.Habilitacao =>
            string.Equals(Codigo, FormularioProcesso.CodigoFaseHabilitacao, StringComparison.Ordinal),
        _ => false,
    };

    /// <summary>Início da janela, sempre em UTC (<c>Offset</c> zero).</summary>
    public DateTimeOffset? Inicio { get; private set; }

    /// <summary>Fim da janela, sempre em UTC (<c>Offset</c> zero).</summary>
    public DateTimeOffset? Fim { get; private set; }

    /// <summary>
    /// Se a fase promete parecer individual por candidato: o parecer sai na execução, e é dele que o
    /// candidato tira fundamento para recorrer.
    /// </summary>
    public bool EmiteParecerIndividual { get; private set; }

    private readonly List<TipoMotivoDeDecisao> _tiposDeDecisao = [];

    /// <summary>Os tipos de decisão que a fase declara produzir, em ordem alfabética ordinal e sem repetição.</summary>
    public IReadOnlyList<TipoMotivoDeDecisao> TiposDeDecisao => _tiposDeDecisao.AsReadOnly();

    /// <summary>A regra de homologação das inscrições que a fase declara; ausente quando a fase não homologa.</summary>
    public RegraDeHomologacaoDasInscricoes? RegraDeHomologacao { get; private set; }

    /// <summary>
    /// Se a fase declara ao menos um produto com papel: decide o piso mínimo havendo vagas e é
    /// pré-condição de recurso.
    /// </summary>
    public bool ProduzResultado => CicloRecursal.ProduzResultado(ParesDeAto());

    /// <summary>Abre ciclo recursal: publica ao menos um resultado preliminar.</summary>
    internal bool PublicaResultadoPreliminar => CicloRecursal.PublicaComPapel(ParesDeAto(), PapelDaPublicacaoPrevista.Preliminar);

    /// <summary>Encerra ciclo recursal: publica ao menos um resultado definitivo.</summary>
    internal bool PublicaResultadoDefinitivo => CicloRecursal.PublicaComPapel(ParesDeAto(), PapelDaPublicacaoPrevista.Definitivo);

    private IEnumerable<(string AtoCodigo, PapelDaPublicacaoPrevista? Papel)> ParesDeAto() =>
        _produtos.Select(static p => (p.AtoCodigo, p.Papel));

    /// <summary>Presença = a fase admite recurso (0..1).</summary>
    public RegraRecursoFase? RegraRecurso { get; private set; }

    private readonly List<ProdutoDaFase> _produtos = [];

    /// <summary>Tudo o que a fase publica, com o papel de cada publicação (0..*).</summary>
    public IReadOnlyCollection<ProdutoDaFase> Produtos => _produtos.AsReadOnly();

    private readonly List<BancaRequerida> _bancasRequeridas = [];

    /// <summary>As bancas que a fase requer (0..*), em ordem canônica: a posição de cada uma é a que as recusas apontam em <c>bancasRequeridas[p]</c>.</summary>
    public IReadOnlyCollection<BancaRequerida> BancasRequeridas => [.. OrdemCanonicaDaConfiguracao.BancasDaFase(_bancasRequeridas)];

    private FaseCronograma() { }

    /// <summary>
    /// Cria uma fase do cronograma validando as invariantes locais (janela × <see cref="OrigemData"/>,
    /// produtos × parecer × regra de recurso). Unicidade de ordem e de origem, precedência entre fases e
    /// bicondicional fase × etapa são da raiz (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>).
    /// </summary>
    /// <remarks>
    /// Os valores são conferidos antes de qualquer filha ser vinculada: numa criação recusada, produtos,
    /// bancas e regra de recurso continuam sem fase. As mensagens citam o código como chegou, sem aparar.
    /// </remarks>
    public static Result<FaseCronograma> Criar(
        ChaveLocal chave,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ConferenciaDaDeclaracaoDaFase.ExigirDeclaracaoPresente(origem, cronograma, publicacoes, bancasRequeridas);
        ConferenciaDaDeclaracaoDaFase.ExigirValoresDeCriacao(cronograma.Ordem, origem.OrigemId, origem.OrigemData);

        List<FieldError> violacoes = ConferenciaDaDeclaracaoDaFase.Violacoes(origem, cronograma, publicacoes, bancasRequeridas, FrozenSet<string>.Empty);
        if (violacoes.Count > 0)
        {
            return Result<FaseCronograma>.ValidationFailure(violacoes);
        }

        return Result<FaseCronograma>.Success(Adotar(chave, origem, cronograma, publicacoes, bancasRequeridas));
    }

    /// <summary>
    /// Adota a declaração de um documento lido como a fase está declarada, <b>sem conferi-la</b>: a recusa da declaração é do
    /// congelamento, que a lê pela <see cref="Declaracao()"/>. A fase nasce com identidade nova.
    /// </summary>
    internal static FaseCronograma Adotar(
        ChaveLocal chave,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ConferenciaDaDeclaracaoDaFase.ExigirDeclaracaoPresente(origem, cronograma, publicacoes, bancasRequeridas);

        FaseCronograma fase = new() { Chave = chave };
        fase.AdotarDeclaracao(origem, cronograma, publicacoes, bancasRequeridas);

        return fase;
    }

    /// <summary>
    /// Reidrata uma fase de uma <see cref="VersaoConfiguracao"/> congelada, <b>preservando o
    /// <see cref="EntityBase.Id"/></b>: os documentos exigidos e a referência temporal dos fatos apontam
    /// para a fase por esse Id, que a reidratação preserva.
    /// </summary>
    /// <remarks>
    /// Reidratar não é criar: as guardas são a última linha contra erro de programação, e a revalidação
    /// de negócio já rodou em <see cref="Criar"/>.
    /// </remarks>
    public static FaseCronograma Reidratar(
        ChaveLocal chave,
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ConferenciaDaDeclaracaoDaFase.ExigirDeclaracaoPresente(origem, cronograma, publicacoes, bancasRequeridas);
        ConferenciaDaDeclaracaoDaFase.ExigirIdCongelado(id);
        ConferenciaDaDeclaracaoDaFase.ExigirAncoraCoerente(publicacoes.Produtos, publicacoes.RegraRecurso);
        ConferenciaDaDeclaracaoDaFase.ExigirCandidatosAtendidosCoerentes(bancasRequeridas);

        FaseCronograma fase = new() { Id = id, Chave = chave };
        fase.AdotarDeclaracao(origem, cronograma, publicacoes, bancasRequeridas);

        return fase;
    }

    private void AdotarDeclaracao(
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        DefinirOrigemCanonica(origem);
        DefinirCronograma(cronograma);
        EmiteParecerIndividual = publicacoes.EmiteParecerIndividual;
        FilhasDoAgregado.Substituir(_tiposDeDecisao, TiposDeDecisaoDeclarados.Canonicos(publicacoes.TiposDeDecisao));
        RegraDeHomologacao = publicacoes.RegraDeHomologacao;
        AdotarFilhas(publicacoes.Produtos, bancasRequeridas, publicacoes.RegraRecurso);
    }

    /// <summary>
    /// A fase como está agora, lida como se acabasse de ser declarada: a hidratação do EF materializa estado
    /// violado sem passar por fábrica, e as recusas da declaração o mostram. As listas da fase que a leitura do documento
    /// deixou sem alguma parte (<paramref name="colecoesComParteRecusada"/>) ficam sem julgar nas regras que as quantificam.
    /// </summary>
    public FaseDeclarada Declaracao(IReadOnlySet<string>? colecoesComParteRecusada = null) => FaseDeclarada.Declarar(
        Id,
        OrigemCanonica(),
        DecisoesDeCronograma(),
        new PublicacoesDaFase([.. OrdemCanonicaDaConfiguracao.ProdutosDaFase(_produtos)], EmiteParecerIndividual, RegraRecurso, [.. _tiposDeDecisao.Select(static tipo => tipo.Codigo())], RegraDeHomologacao),
        [.. BancasRequeridas],
        colecoesComParteRecusada);

    /// <summary>O que da fase entra no resultado preliminar que ela publica (ver <see cref="ValueObjects.EntradaDoPreliminar"/>).</summary>
    public EntradaDoPreliminar EntradaDoPreliminar() => EntradaDoPreliminarDaFase.De(this);

    /// <summary>Refaz a cópia de código, sigla e nome da unidade que a fase já tem, pelo valor vivo do cadastro.</summary>
    internal void RecopiarUnidadeResponsavel(UnidadeResponsavelSnapshot viva)
    {
        ArgumentNullException.ThrowIfNull(viva);

        if (UnidadeResponsavel?.OrigemId == viva.OrigemId)
        {
            DefinirUnidadeResponsavel(viva);
        }
    }

    private void DefinirUnidadeResponsavel(UnidadeResponsavelSnapshot? unidade) =>
        UnidadeResponsavel = unidade is null ? null : new UnidadeResponsavelSnapshot(unidade.OrigemId, unidade.Codigo, unidade.Sigla, unidade.Nome);

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Atualiza os dados da MESMA fase (mesmo <see cref="EntityBase.Id"/>) em vez de recriá-la, copiando-os
    /// de <paramref name="origem"/>. Serve à redefinição do cronograma, que reconcilia por
    /// <see cref="FaseCanonicaOrigemId"/> e na qual <see cref="Ordem"/> pode mudar.
    /// </summary>
    /// <remarks>
    /// Recriar a fase trocaria o <c>Id</c> e o <c>CreatedAt</c> de uma fase que nunca deixou de existir, e
    /// derrubaria as referências por Id (documentos exigidos, referência temporal de fatos). Os produtos
    /// seguem o mesmo cuidado, reconciliados pelo par ato e papel (<see cref="ProdutosPublicados"/>), e as bancas pelo id
    /// (<see cref="ReconciliacaoDasBancas"/>).
    /// </remarks>
    internal void AtualizarDe(FaseCronograma origem, IReadOnlyList<ProdutoDaFase> produtos)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(produtos);

        DefinirOrigemCanonica(origem.OrigemCanonica());
        DefinirCronograma(origem.DecisoesDeCronograma());
        EmiteParecerIndividual = origem.EmiteParecerIndividual;
        FilhasDoAgregado.Substituir(_tiposDeDecisao, origem._tiposDeDecisao);
        RegraDeHomologacao = origem.RegraDeHomologacao;
        ReconciliarProdutos(produtos);
        ReporBancasRequeridasDe(origem);
        ReporRegraDeRecursoDe(origem, produtos);
    }

    private OrigemCanonicaDaFase OrigemCanonica() => new(
        FaseCanonicaOrigemId, Codigo, OrigemData, AgrupaEtapas, PermiteComplementacao, ColetaInscricao, ColetaSolicitacaoIsencao)
    { Nome = TipoFaseNome };

    private DecisoesDeCronogramaDaFase DecisoesDeCronograma() =>
        new(Ordem, Inicio, Fim, UnidadeResponsavel) { NoMesmoMomentoDaAnterior = NoMesmoMomentoDaAnterior };

    private void DefinirOrigemCanonica(OrigemCanonicaDaFase origem)
    {
        FaseCanonicaOrigemId = origem.OrigemId;
        Codigo = origem.Codigo;
        TipoFaseNome = origem.Nome.Trim();
        OrigemData = origem.OrigemData;
        AgrupaEtapas = origem.AgrupaEtapas;
        PermiteComplementacao = origem.PermiteComplementacao;
        ColetaInscricao = origem.ColetaInscricao;
        ColetaSolicitacaoIsencao = origem.ColetaSolicitacaoIsencao;
    }

    private void DefinirCronograma(DecisoesDeCronogramaDaFase cronograma)
    {
        Ordem = cronograma.Ordem;
        NoMesmoMomentoDaAnterior = cronograma.NoMesmoMomentoDaAnterior;
        Inicio = JanelaDeTempo.EmUtc(cronograma.Inicio);
        Fim = JanelaDeTempo.EmUtc(cronograma.Fim);
        DefinirUnidadeResponsavel(cronograma.UnidadeResponsavel);
    }

    private void ReconciliarProdutos(IReadOnlyList<ProdutoDaFase> chegados)
    {
        ProdutosReconciliados<ProdutoDaFase> reconciliados = ProdutosPublicados
            .ReconciliarPreservandoIdentidadeMesmoComTrocaDePapel(_produtos, chegados, static ato => ato);

        AdotarProdutos(reconciliados.Produtos);
    }

    private void ReporBancasRequeridasDe(FaseCronograma origem) => AdotarBancasRequeridas(origem.BancasRequeridas);

    private void ReporRegraDeRecursoDe(FaseCronograma origem, IReadOnlyList<ProdutoDaFase> produtos)
    {
        RegraRecurso = origem.RegraRecurso;
        if (RegraRecurso is null)
        {
            return;
        }

        RegraRecurso.VincularFase(Id);
        AncoraDoRecursoDaFase.Reancorar(RegraRecurso, produtos, _produtos);
    }

    private void AdotarFilhas(
        IReadOnlyList<ProdutoDaFase> produtos,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        RegraRecursoFase? regraRecurso)
    {
        AdotarProdutos(produtos);
        AdotarBancasRequeridas(bancasRequeridas);

        if (regraRecurso is not null)
        {
            regraRecurso.VincularFase(Id);
            RegraRecurso = regraRecurso;
        }
    }

    private void AdotarProdutos(IEnumerable<ProdutoDaFase> produtos) =>
        FilhasDoAgregado.Substituir(_produtos, produtos, produto => produto.VincularFase(Id));

    private void AdotarBancasRequeridas(IEnumerable<BancaRequerida> bancasRequeridas) =>
        FilhasDoAgregado.Substituir(_bancasRequeridas, ReconciliacaoDasBancas.Reconciliar(_bancasRequeridas, bancasRequeridas), banca => banca.VincularFase(Id));
}
