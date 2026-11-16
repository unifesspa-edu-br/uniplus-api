namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Services;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

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
/// Application (ADR-0042). A alcançabilidade da conclusão do ciclo recursal é da raiz, a única que
/// enxerga o cronograma inteiro.
/// </remarks>
public sealed class FaseCronograma : EntityBase, IParteDoProcesso
{
    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Posição do certame na linha do tempo, única dentro do cronograma.</summary>
    public int Ordem { get; private set; }

    /// <summary>Id (Guid v7) da <c>FaseCanonica</c> viva de origem, no momento do congelamento.</summary>
    public Guid FaseCanonicaOrigemId { get; private set; }

    /// <summary>Código canônico congelado (ex.: <c>"HOMOLOGACAO"</c>).</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Id da unidade que responde pela fase, congelado sem FK (ADR-0061); opcional no rascunho, exigido na publicação.</summary>
    public Guid? UnidadeResponsavelOrigemId { get; private set; }

    public string? UnidadeResponsavelSigla { get; private set; }

    public string? UnidadeResponsavelNome { get; private set; }

    /// <summary>A unidade responsável como valor, ou <see langword="null"/> quando a fase ainda não a tem.</summary>
    public UnidadeResponsavelSnapshot? UnidadeResponsavel =>
        UnidadeResponsavelOrigemId is { } origemId
            ? new UnidadeResponsavelSnapshot(origemId, UnidadeResponsavelSigla!, UnidadeResponsavelNome!)
            : null;

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
    /// Código canônico da fase que conclui o ciclo recursal desta, quando a própria fase não publica a
    /// definitiva da matéria que abriu.
    /// </summary>
    public string? FaseConcluinteCodigo { get; private set; }

    /// <summary>
    /// Se a fase promete parecer individual por candidato: o parecer sai na execução, e é dele que o
    /// candidato tira fundamento para recorrer.
    /// </summary>
    public bool EmiteParecerIndividual { get; private set; }

    /// <summary>
    /// Se a fase declara ao menos um produto com papel: decide o piso mínimo havendo vagas e é
    /// pré-condição de recurso.
    /// </summary>
    public bool ProduzResultado => _produtos.Exists(static p => p.Papel is not null);

    /// <summary>Abre ciclo recursal: publica ao menos um resultado preliminar.</summary>
    internal bool PublicaResultadoPreliminar =>
        _produtos.Exists(static p => p.Papel == PapelProdutoFase.Preliminar);

    /// <summary>Encerra ciclo recursal: publica ao menos um resultado definitivo.</summary>
    internal bool PublicaResultadoDefinitivo =>
        _produtos.Exists(static p => p.Papel == PapelProdutoFase.Definitivo);

    /// <summary>Se a fase publica o resultado definitivo desse tipo de ato (a matéria é o tipo).</summary>
    internal bool PublicaResultadoDefinitivoDe(string atoCodigo) =>
        _produtos.Exists(p => p.Papel == PapelProdutoFase.Definitivo
            && string.Equals(p.AtoCodigo, atoCodigo, StringComparison.Ordinal));

    /// <summary>
    /// Os tipos de ato cujo preliminar a fase publica sem publicar o definitivo do mesmo tipo:
    /// são as matérias cujo ciclo recursal ela deixa para a fase concluinte fechar.
    /// </summary>
    public IEnumerable<string> TiposComCicloAberto() =>
        CicloRecursal.TiposComCicloAberto(_produtos.Select(static p => (p.AtoCodigo, p.Papel)));

    /// <summary>Os tipos de ato cujo preliminar e cujo definitivo a própria fase publica.</summary>
    internal IEnumerable<string> TiposFechadosNaPropriaFase() =>
        CicloRecursal.TiposFechadosNoProprioLugar(_produtos.Select(static p => (p.AtoCodigo, p.Papel)));

    /// <summary>Presença = a fase admite recurso (0..1).</summary>
    public RegraRecursoFase? RegraRecurso { get; private set; }

    private readonly List<ProdutoDaFase> _produtos = [];

    /// <summary>Tudo o que a fase publica, com o papel de cada publicação (0..*).</summary>
    public IReadOnlyCollection<ProdutoDaFase> Produtos => _produtos.AsReadOnly();

    private readonly List<BancaRequerida> _bancasRequeridas = [];
    public IReadOnlyCollection<BancaRequerida> BancasRequeridas => _bancasRequeridas.AsReadOnly();

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
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ConferenciaDaDeclaracaoDaFase.ExigirDeclaracaoPresente(origem, cronograma, publicacoes, bancasRequeridas);
        ConferenciaDaDeclaracaoDaFase.ExigirValoresDeCriacao(cronograma.Ordem, origem.OrigemId, origem.OrigemData);

        List<FieldError> violacoes = ConferenciaDaDeclaracaoDaFase.Violacoes(origem, cronograma, publicacoes, bancasRequeridas);
        if (violacoes.Count > 0)
        {
            return Result<FaseCronograma>.ValidationFailure(violacoes);
        }

        FaseCronograma fase = new();
        fase.AdotarDeclaracao(origem, cronograma, publicacoes, bancasRequeridas);

        return Result<FaseCronograma>.Success(fase);
    }

    /// <summary>
    /// Reidrata uma fase de uma <see cref="VersaoConfiguracao"/> congelada, <b>preservando o
    /// <see cref="EntityBase.Id"/></b>: os documentos exigidos e a referência temporal dos fatos apontam
    /// para a fase por esse Id, e a sombra de verificação da restauração começa sem fase viva a reconciliar.
    /// </summary>
    /// <remarks>
    /// Reidratar não é criar: as guardas são a última linha contra erro de programação, e a revalidação
    /// de negócio já rodou em <see cref="Criar"/>.
    /// </remarks>
    public static FaseCronograma Reidratar(
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ConferenciaDaDeclaracaoDaFase.ExigirDeclaracaoPresente(origem, cronograma, publicacoes, bancasRequeridas);
        ConferenciaDaDeclaracaoDaFase.ExigirIdCongelado(id);
        ConferenciaDaDeclaracaoDaFase.ExigirAncoraCoerente(origem.Codigo, publicacoes.Produtos, publicacoes.RegraRecurso);
        ConferenciaDaDeclaracaoDaFase.ExigirRecorteCoerente(origem.Codigo, bancasRequeridas);

        FaseCronograma fase = new() { Id = id };
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
        AdotarFilhas(publicacoes.Produtos, bancasRequeridas, publicacoes.RegraRecurso);
    }

    /// <summary>
    /// <see cref="AncoraDoRecursoDaFase.Violacao"/> sobre o estado ATUAL da fase, a forma que
    /// <c>ProcessoSeletivo</c> usa porque a hidratação do EF materializa estado violado sem passar por fábrica.
    /// </summary>
    internal FieldError? ViolacaoDaAncoraDoRecurso() =>
        AncoraDoRecursoDaFase.Violacao(Codigo, _produtos, RegraRecurso);

    /// <summary><see cref="RecorteDeCompetenciaDasBancas.Violacoes"/> sobre o estado ATUAL, pelo mesmo motivo.</summary>
    internal IReadOnlyList<FieldError> ViolacoesDoRecorteDeCompetencia() =>
        RecorteDeCompetenciaDasBancas.Violacoes(Codigo, _bancasRequeridas);

    /// <summary>O que da fase entra no resultado preliminar que ela publica (ver <see cref="ValueObjects.EntradaDoPreliminar"/>).</summary>
    public EntradaDoPreliminar EntradaDoPreliminar() => EntradaDoPreliminarDaFase.De(this);

    /// <summary>Refaz a cópia de sigla e nome da unidade que a fase já tem, pelo valor vivo do cadastro.</summary>
    internal void RecopiarUnidadeResponsavel(UnidadeResponsavelSnapshot viva)
    {
        ArgumentNullException.ThrowIfNull(viva);

        if (UnidadeResponsavelOrigemId == viva.OrigemId)
        {
            DefinirUnidadeResponsavel(viva);
        }
    }

    private void DefinirUnidadeResponsavel(UnidadeResponsavelSnapshot? unidade)
    {
        UnidadeResponsavelOrigemId = unidade?.OrigemId;
        UnidadeResponsavelSigla = unidade?.Sigla;
        UnidadeResponsavelNome = unidade?.Nome;
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Atualiza os dados da MESMA fase (mesmo <see cref="EntityBase.Id"/>) em vez de recriá-la, copiando-os
    /// de <paramref name="origem"/>. Serve à reposição da configuração congelada, que reconcilia por
    /// <see cref="Ordem"/>, e à redefinição ao vivo do cronograma, que reconcilia por
    /// <see cref="FaseCanonicaOrigemId"/> e na qual <see cref="Ordem"/> pode mudar.
    /// </summary>
    /// <remarks>
    /// Recriar a fase trocaria o <c>Id</c> e o <c>CreatedAt</c> de uma fase que nunca deixou de existir, e
    /// derrubaria as referências por Id (documentos exigidos, referência temporal de fatos). Os produtos
    /// seguem o mesmo cuidado, reconciliados pelo par ato e papel (<see cref="ProdutosPublicados"/>). Banca
    /// e recorte não precisam: a chave do índice das categorias inclui o id da banca, que sempre chega nova.
    /// </remarks>
    internal void AtualizarDe(FaseCronograma origem, IReadOnlyList<ProdutoDaFase> produtos)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(produtos);

        DefinirOrigemCanonica(origem.OrigemCanonica());
        DefinirCronograma(origem.DecisoesDeCronograma());
        EmiteParecerIndividual = origem.EmiteParecerIndividual;
        ReconciliarProdutos(produtos);
        ReporBancasRequeridasDe(origem);
        ReporRegraDeRecursoDe(origem, produtos);
    }

    private OrigemCanonicaDaFase OrigemCanonica() => new(
        FaseCanonicaOrigemId, Codigo, OrigemData, AgrupaEtapas, PermiteComplementacao, ColetaInscricao, ColetaSolicitacaoIsencao);

    private DecisoesDeCronogramaDaFase DecisoesDeCronograma() =>
        new(Ordem, Inicio, Fim, FaseConcluinteCodigo, UnidadeResponsavel);

    private void DefinirOrigemCanonica(OrigemCanonicaDaFase origem)
    {
        FaseCanonicaOrigemId = origem.OrigemId;
        Codigo = origem.Codigo.Trim();
        OrigemData = origem.OrigemData;
        AgrupaEtapas = origem.AgrupaEtapas;
        PermiteComplementacao = origem.PermiteComplementacao;
        ColetaInscricao = origem.ColetaInscricao;
        ColetaSolicitacaoIsencao = origem.ColetaSolicitacaoIsencao;
    }

    private void DefinirCronograma(DecisoesDeCronogramaDaFase cronograma)
    {
        Ordem = cronograma.Ordem;
        Inicio = JanelaDaFase.EmUtc(cronograma.Inicio);
        Fim = JanelaDaFase.EmUtc(cronograma.Fim);
        FaseConcluinteCodigo = NormalizarCodigo(cronograma.FaseConcluinteCodigo);
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
        FilhasDoAgregado.Substituir(_bancasRequeridas, bancasRequeridas, banca => banca.VincularFase(Id));

    // Texto em branco e ausência são o mesmo estado ("esta fase não aponta concluinte"); sem normalizar, a
    // mesma configuração produziria bytes canônicos distintos conforme o cliente enviasse null ou "".
    private static string? NormalizarCodigo(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
}
