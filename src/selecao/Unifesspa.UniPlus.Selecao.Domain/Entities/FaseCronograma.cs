namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Services;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using ValueObjects;

/// <summary>
/// Uma fase do cronograma de um <see cref="ProcessoSeletivo"/> (1..*, Story #851) — o
/// eixo <b>temporal</b> do certame (janela, ordem, unidade responsável, o que a fase
/// publica), distinto do eixo de <b>pontuação</b> (<see cref="EtapaProcesso"/>).
/// Snapshot-copy (ADR-0061) de uma <c>FaseCanonica</c> do módulo Configuração no
/// momento em que entrou no cronograma — sem FK para o cadastro vivo.
/// </summary>
/// <remarks>
/// <para>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>: a configuração em rascunho é substituível por inteiro
/// (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>).
/// </para>
/// <para>
/// <b>A janela é guardada em UTC</b>, qualquer que seja o offset com que o instante
/// chegou: <see cref="Inicio"/> e <see cref="Fim"/> passam por
/// <see cref="DateTimeOffset.ToUniversalTime"/> em todo caminho de escrita, preservando o
/// instante. O offset escolhido por quem escreve é forma de transporte, não informação de
/// domínio — e a coluna <c>timestamp with time zone</c> só aceita a representação em UTC.
/// </para>
/// <para>
/// <b>O que a fase publica é declarado, não copiado do cadastro.</b> A coleção
/// <see cref="Produtos"/> traz o código de cada ato e o papel de cada publicação no ciclo
/// recursal; <see cref="ProduzResultado"/> deriva dela, e a âncora do prazo de recurso
/// referencia um deles — <see cref="RegraRecursoFase.ProdutoAncoraId"/> é sempre um produto
/// preliminar desta fase. Que o papel só caiba em ato que é resultado no catálogo é I/O —
/// Application (ADR-0042).
/// </para>
/// <para>
/// <b>Invariantes que esta factory prova sozinha</b> (não dependem de leitura externa):
/// janela obrigatória/opcional conforme <see cref="OrigemData"/> (CA-07), janela não
/// invertida, o mesmo ato declarado uma única vez, o parecer individual sustentado por
/// alguma publicação de resultado, e a invariante de <see cref="RegraRecursoFase"/> que
/// depende da fase-mãe — a âncora do prazo é um produto preliminar desta fase. A
/// alcançabilidade da conclusão do ciclo recursal é da <b>raiz</b>
/// (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>): só ela enxerga o cronograma
/// inteiro.
/// </para>
/// </remarks>
public sealed class FaseCronograma : EntityBase, IParteDoProcesso
{
    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Posição do certame na linha do tempo — única dentro do cronograma (CA-06).</summary>
    public int Ordem { get; private set; }

    /// <summary>Id (Guid v7) da <c>FaseCanonica</c> viva de origem, no momento do congelamento.</summary>
    public Guid FaseCanonicaOrigemId { get; private set; }

    /// <summary>Código canônico congelado (ex.: <c>"HOMOLOGACAO"</c>) — snapshot-copy.</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Id da unidade que responde pela fase, congelado sem FK (ADR-0061) — opcional no rascunho, exigido na publicação.</summary>
    public Guid? UnidadeResponsavelOrigemId { get; private set; }

    /// <summary>Sigla da unidade responsável, copiada ao definir o cronograma.</summary>
    public string? UnidadeResponsavelSigla { get; private set; }

    /// <summary>Nome da unidade responsável, copiado ao definir o cronograma.</summary>
    public string? UnidadeResponsavelNome { get; private set; }

    /// <summary>A unidade responsável como valor, ou <see langword="null"/> quando a fase ainda não a tem.</summary>
    public UnidadeResponsavelSnapshot? UnidadeResponsavel =>
        UnidadeResponsavelOrigemId is { } origemId
            ? new UnidadeResponsavelSnapshot(origemId, UnidadeResponsavelSigla!, UnidadeResponsavelNome!)
            : null;

    /// <summary>Quando a data desta fase é fixada (no edital ou na execução) — snapshot-copy; decide a obrigatoriedade da janela (CA-07).</summary>
    public OrigemDataFase OrigemData { get; private set; }

    /// <summary>Se esta fase agrupa etapas pontuadas — bicondicional com a existência de etapa (§3.5).</summary>
    public bool AgrupaEtapas { get; private set; }

    public bool PermiteComplementacao { get; private set; }

    /// <summary>Se a fase coleta inscrição — decide o piso mínimo quando <see cref="OrigemCandidatos.InscricaoPropria"/>.</summary>
    public bool ColetaInscricao { get; private set; }

    /// <summary>Verdadeiro quando a fase abre a janela de pedido de isenção da taxa.</summary>
    public bool ColetaSolicitacaoIsencao { get; private set; }

    private static readonly FinalidadeFormulario[] FinalidadesNaOrdemDeApresentacao =
        [FinalidadeFormulario.Inscricao, FinalidadeFormulario.IsencaoTaxa, FinalidadeFormulario.Habilitacao];

    /// <summary>
    /// As finalidades cujo formulário se responde nesta fase: a inscrição na fase que a coleta, a
    /// isenção na que abre o pedido e a habilitação na fase de habilitação. Inscrição e isenção podem
    /// dividir a mesma fase.
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

    /// <summary>
    /// Início da janela, sempre em UTC (<c>Offset</c> zero) — ver o <c>&lt;remarks&gt;</c> da
    /// classe sobre a representação da janela.
    /// </summary>
    public DateTimeOffset? Inicio { get; private set; }

    /// <summary>Fim da janela, sempre em UTC (<c>Offset</c> zero).</summary>
    public DateTimeOffset? Fim { get; private set; }

    /// <summary>
    /// Código canônico da fase que conclui o ciclo recursal desta, quando a própria fase
    /// não publica a definitiva da matéria que abriu.
    /// </summary>
    public string? FaseConcluinteCodigo { get; private set; }

    /// <summary>
    /// Se a fase promete parecer individual por candidato. É a promessa de que existirá; o
    /// parecer é produzido na execução, e é ele que dá ao candidato fundamento claro para
    /// recorrer.
    /// </summary>
    public bool EmiteParecerIndividual { get; private set; }

    /// <summary>
    /// Se a fase produz resultado — decide o piso mínimo havendo vagas (§3.4) e é
    /// pré-condição de recurso. Deriva de <see cref="Produtos"/>: a fase produz resultado
    /// quando declara ao menos um produto com papel.
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

    /// <summary>Presença = a fase admite recurso (0..1, §3.6).</summary>
    public RegraRecursoFase? RegraRecurso { get; private set; }

    private readonly List<ProdutoDaFase> _produtos = [];

    /// <summary>Tudo o que a fase publica, com o papel de cada publicação (0..*).</summary>
    public IReadOnlyCollection<ProdutoDaFase> Produtos => _produtos.AsReadOnly();

    private readonly List<BancaRequerida> _bancasRequeridas = [];
    public IReadOnlyCollection<BancaRequerida> BancasRequeridas => _bancasRequeridas.AsReadOnly();

    private FaseCronograma() { }

    /// <summary>
    /// Cria uma fase do cronograma, validando as invariantes locais (janela ×
    /// <see cref="OrigemData"/>, produtos × parecer × regra de recurso). A unicidade de
    /// <see cref="Ordem"/> e de <see cref="FaseCanonicaOrigemId"/> dentro do cronograma, a
    /// precedência entre fases, a bicondicional fase×etapa e a alcançabilidade da conclusão
    /// do ciclo recursal são validadas pela raiz
    /// (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>), que tem acesso ao
    /// cronograma inteiro e às etapas.
    /// </summary>
    /// <remarks>
    /// Os valores recebidos são conferidos antes de qualquer filha ser vinculada: numa criação
    /// recusada, produtos, bancas e regra de recurso continuam sem fase. As mensagens citam o código
    /// como chegou, sem aparar. As recusas de negócio acumulam (ADR-0125) porque são independentes
    /// entre si; a ordem, a origem canônica e a origem da data são erro de programação e só lançam.
    /// </remarks>
    public static Result<FaseCronograma> Criar(
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ExigirDeclaracaoPresente(origem, cronograma, publicacoes, bancasRequeridas);
        ExigirValoresDeCriacao(cronograma.Ordem, origem.OrigemId, origem.OrigemData);

        List<FieldError> violacoes = ViolacoesDaDeclaracao(origem, cronograma, publicacoes, bancasRequeridas);
        if (violacoes.Count > 0)
        {
            return Result<FaseCronograma>.ValidationFailure(violacoes);
        }

        FaseCronograma fase = new();
        fase.AdotarDeclaracao(origem, cronograma, publicacoes, bancasRequeridas);

        return Result<FaseCronograma>.Success(fase);
    }

    /// <summary>
    /// Reidrata uma fase a partir de uma <see cref="VersaoConfiguracao"/> congelada,
    /// <b>preservando o <see cref="EntityBase.Id"/></b> — Story #554 (PR #903, bump 1.2),
    /// achado de revisão: duas referências cruzadas do envelope apontam para
    /// <c>FaseCronograma.Id</c> (<c>documentosExigidos.exigencias[].exigidoNaFaseId</c> e
    /// <c>documentosExigidos.referenciaTemporalFatos.faseId</c>). A reconciliação por
    /// <see cref="Ordem"/> em <c>ProcessoSeletivo.AplicarGrafo</c> só preserva o Id quando
    /// existe uma fase VIVA rastreada com a mesma Ordem — a sombra de verificação
    /// ("prova primeiro, aplica depois", <c>RestauradorDeConfiguracao</c>) começa vazia,
    /// então sem preservar o Id aqui a prova de round-trip lançaria (ou reprovaria) para
    /// qualquer configuração com gatilho <c>FAIXA_ETARIA</c> ancorado em fase, sempre.
    /// </summary>
    /// <remarks>
    /// Reidratar não é criar: as guardas aqui são a última linha contra erro de
    /// programação, não revalidação de negócio (essa já rodou em <see cref="Criar"/>,
    /// quando a fase foi escrita pela primeira vez).
    /// </remarks>
    public static FaseCronograma Reidratar(
        Guid id,
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ExigirDeclaracaoPresente(origem, cronograma, publicacoes, bancasRequeridas);
        ExigirIdCongelado(id);
        ExigirAncoraCoerente(origem.Codigo, publicacoes.Produtos, publicacoes.RegraRecurso);
        ExigirRecorteCoerente(origem.Codigo, bancasRequeridas);

        FaseCronograma fase = new() { Id = id };
        fase.AdotarDeclaracao(origem, cronograma, publicacoes, bancasRequeridas);

        return fase;
    }

    private static void ExigirDeclaracaoPresente(
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(publicacoes);
        ExigirCodigoEFilhas(origem.Codigo, publicacoes.Produtos, bancasRequeridas);
    }

    private static void ExigirCodigoEFilhas(
        string codigo, IReadOnlyList<ProdutoDaFase> produtos, IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);
    }

    private static void ExigirValoresDeCriacao(int ordem, Guid faseCanonicaOrigemId, OrigemDataFase origemData)
    {
        if (ordem <= 0)
        {
            throw new ArgumentException("A ordem da fase deve ser maior que zero.", nameof(ordem));
        }

        if (faseCanonicaOrigemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem da fase canônica é obrigatório.", nameof(faseCanonicaOrigemId));
        }

        if (origemData == OrigemDataFase.Nenhuma)
        {
            throw new ArgumentException("A origem da data da fase é obrigatória.", nameof(origemData));
        }
    }

    private static void ExigirIdCongelado(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A fase reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }
    }

    // A âncora congelada é reposta como estava — é ela que o ato já publicado resolve de volta
    // (UNI-REQ-0093). Pelo MESMO predicado de Criar: uma âncora reposta sobre produto definitivo é tão
    // incoerente quanto uma escrita assim pela primeira vez, e checar só a pertinência deixaria passar
    // metade.
    private static void ExigirAncoraCoerente(
        string codigo, IReadOnlyList<ProdutoDaFase> produtos, RegraRecursoFase? regraRecurso)
    {
        if (AncoraDoRecursoDaFase.Violacao(codigo, produtos, regraRecurso) is { } violacaoDaAncora)
        {
            throw new ArgumentException(violacaoDaAncora.Error.Message, nameof(regraRecurso));
        }
    }

    // Pelo MESMO predicado de Criar: um recorte reposto que não distingue duas bancas do mesmo tipo é
    // tão incoerente quanto um escrito assim pela primeira vez.
    private static void ExigirRecorteCoerente(string codigo, IReadOnlyList<BancaRequerida> bancasRequeridas)
    {
        if (RecorteDeCompetenciaDasBancas.Violacoes(codigo, bancasRequeridas) is [{ } violacaoDoRecorte, ..])
        {
            throw new ArgumentException(violacaoDoRecorte.Error.Message, nameof(bancasRequeridas));
        }
    }

    // As recusas de negócio acumulam (ADR-0125) e nenhuma decide o que checar a partir de outra. Não há
    // passada pré-I/O para o handler chamar antes: a origem da data é cópia do CADASTRO e os produtos só
    // existem depois de o catálogo de tipos de ato ser consultado. A janela invertida acumula aqui, e não
    // no validator, para a resposta trazê-la junto das outras violações da mesma fase.
    private static List<FieldError> ViolacoesDaDeclaracao(
        OrigemCanonicaDaFase origem,
        DecisoesDeCronogramaDaFase cronograma,
        PublicacoesDaFase publicacoes,
        IReadOnlyList<BancaRequerida> bancasRequeridas) =>
    [
        .. ViolacoesDaJanela(origem.Codigo, origem.OrigemData, cronograma),
        .. ViolacoesDasPublicacoes(origem.Codigo, publicacoes),
        .. RecorteDeCompetenciaDasBancas.Violacoes(origem.Codigo, bancasRequeridas),
    ];

    // A janela é fixada em UTC antes de montar as mensagens, para que a violação e o valor gravado falem
    // da mesma representação do instante.
    private static List<FieldError> ViolacoesDaJanela(
        string codigo, OrigemDataFase origemData, DecisoesDeCronogramaDaFase cronograma)
    {
        DateTimeOffset? inicio = EmUtc(cronograma.Inicio);
        DateTimeOffset? fim = EmUtc(cronograma.Fim);
        FieldError?[] violacoes =
        [
            JanelaInvertida(codigo, inicio, fim),
            JanelaObrigatoriaNoEdital(codigo, origemData, inicio, fim),
        ];

        return [.. violacoes.OfType<FieldError>()];
    }

    // Vale independentemente da origem da data: uma fase NA_EXECUCAO com janela declarada não escapa da
    // checagem de coerência interna.
    private static FieldError? JanelaInvertida(string codigo, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        inicio is { } inicioValor && fim is { } fimValor && fimValor < inicioValor
            ? new FieldError("fim", new DomainError(
                "FaseCronograma.JanelaInvertida",
                $"A fase '{codigo}' tem o fim da janela ({fimValor:O}) antes do início ({inicioValor:O})."))
            : null;

    // CA-07: NO_EDITAL exige janela; NA_EXECUCAO aceita "sem data" como estado válido — a data não é
    // fixada no edital, e a unidade responsável a define na execução (§3.2).
    private static FieldError? JanelaObrigatoriaNoEdital(
        string codigo, OrigemDataFase origemData, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        origemData == OrigemDataFase.NoEdital && (inicio is null || fim is null)
            ? new FieldError(null, new DomainError(
                "FaseCronograma.JanelaObrigatoriaEmDataNoEdital",
                $"A fase '{codigo}' tem a data fixada no edital e exige início e fim da janela."))
            : null;

    private static List<FieldError> ViolacoesDasPublicacoes(string codigo, PublicacoesDaFase publicacoes)
    {
        FieldError?[] violacoes =
        [
            AtoDuplicado(codigo, publicacoes.Produtos),
            ParecerIndividualSemResultado(codigo, publicacoes),
            AncoraDoRecursoDaFase.Violacao(codigo, publicacoes.Produtos, publicacoes.RegraRecurso),
        ];

        return [.. violacoes.OfType<FieldError>()];
    }

    // O par ato + papel é a chave natural do produto dentro da fase, e o índice único da tabela a espelha.
    // A chave é o par, e não o ato sozinho, porque o catálogo nomeia a MATÉRIA, não o papel: "Gabarito"
    // é um código só, publicado como preliminar — que abre o ciclo recursal — e como definitivo, que o
    // encerra.
    private static FieldError? AtoDuplicado(string codigo, IReadOnlyList<ProdutoDaFase> produtos) =>
        ProdutosPublicados.PrimeiroParRepetido(produtos, static ato => ato) is { } duplicado
            ? new FieldError("produtos", new DomainError(
                "FaseCronograma.AtoDuplicadoNaFase",
                $"A fase '{codigo}' declara o ato '{duplicado.AtoCodigo}' mais de uma vez no mesmo papel — o par ato e papel é declarado uma única vez por fase."))
            : null;

    // O parecer individual é o que dá ao candidato fundamento claro para recorrer. Sem publicação de
    // resultado não há decisão a contestar nem prazo a correr, e a promessa ficaria sem objeto.
    private static FieldError? ParecerIndividualSemResultado(string codigo, PublicacoesDaFase publicacoes) =>
        publicacoes.EmiteParecerIndividual && !publicacoes.Produtos.Any(static p => p.Papel is not null)
            ? new FieldError("emiteParecerIndividual", new DomainError(
                "FaseCronograma.ParecerIndividualSemResultado",
                $"A fase '{codigo}' promete parecer individual e não publica nenhum resultado — não haveria decisão a fundamentar."))
            : null;

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
    /// <see cref="AncoraDoRecursoDaFase.Violacao"/> sobre o estado ATUAL da fase — a forma que
    /// <c>ProcessoSeletivo</c> usa, porque o estado violado é materializável pela hidratação do EF, que
    /// não passa por fábrica nenhuma.
    /// </summary>
    internal FieldError? ViolacaoDaAncoraDoRecurso() =>
        AncoraDoRecursoDaFase.Violacao(Codigo, _produtos, RegraRecurso);

    /// <summary>
    /// <see cref="RecorteDeCompetenciaDasBancas.Violacoes"/> sobre o estado ATUAL da fase — a forma que
    /// <c>ProcessoSeletivo</c> usa, pelo mesmo motivo de <see cref="ViolacaoDaAncoraDoRecurso"/>.
    /// </summary>
    internal IReadOnlyList<FieldError> ViolacoesDoRecorteDeCompetencia() =>
        RecorteDeCompetenciaDasBancas.Violacoes(Codigo, _bancasRequeridas);

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
    /// Atualiza os dados da MESMA fase (mesmo <see cref="EntityBase.Id"/>) em vez de recriá-la,
    /// copiando-os de <paramref name="origem"/>: usado tanto pela reposição da configuração congelada
    /// (<see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>, reconciliação por
    /// <see cref="Ordem"/> — a instância VIVA rastreada, a do EF, é que precisa sobreviver) quanto pela redefinição ao vivo do cronograma
    /// (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>, reconciliação por
    /// <see cref="FaseCanonicaOrigemId"/> — a identidade estável de uma fase; aqui
    /// <see cref="Ordem"/> PODE mudar, ao contrário do caminho de restauração).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sem esta reconciliação, repor o cronograma faria <c>Clear()</c> + <c>Add</c> de
    /// instâncias novas — DELETE das fases antigas e INSERT das novas na MESMA
    /// transação, com a nova fase reocupando a MESMA <see cref="Ordem"/> da antiga (o
    /// caso comum: a sessão editorial só mudou datas, não renumerou o cronograma).
    /// <b>Esse DELETE+INSERT do mesmo slot de <c>ux_fases_cronograma_processo_ordem</c>
    /// não colide</b>: um índice único sem filtro entra no grafo de comandos do EF Core,
    /// que emite o DELETE liberando o valor antes do INSERT que o quer. Medido contra
    /// Postgres real em <c>EtapaBancaPersistenciaTests</c>, sobre o índice equivalente das
    /// bancas da etapa, e confirmado pela própria mensagem do EF quando o grafo não tem
    /// solução, que nomeia o índice (<c>FaseOrdemPermutacaoPersistenciaTests</c>).
    /// </para>
    /// <para>
    /// <b>O que sustenta a reconciliação é a identidade da linha, não risco de colisão.</b>
    /// Recriar a fase a cada restauração trocaria o <c>Id</c> e o <c>CreatedAt</c> de uma
    /// fase que nunca deixou de existir, e derrubaria toda referência guardada por Id —
    /// documentos exigidos e referência temporal de fatos apontam para a fase pelo Id
    /// congelado. Os produtos exigem o mesmo cuidado pela mesma razão, e são reconciliados
    /// pelo par <see cref="ProdutoDaFase.AtoCodigo"/> + <see cref="ProdutoDaFase.Papel"/>
    /// (<see cref="ProdutosPublicados"/>), reusando a instância rastreada;
    /// <c>bancas_requeridas</c> não é referenciada por Id de fora da fase e por isso segue com
    /// a reposição simples.
    /// </para>
    /// <para>
    /// <b>O recorte de competência viaja junto da sua banca, e não precisa de reconciliação
    /// própria</b>, apesar de <c>ux_categorias_julgadas_codigo</c> existir. A chave daquele
    /// índice inclui o id da banca, e toda banca que chega é instância nova — nem o
    /// decodificador do envelope nem a escrita ao vivo reusam a linha rastreada —, então o
    /// DELETE das categorias antigas e o INSERT das novas nunca disputam o mesmo par. Se um
    /// dia a banca passar a ser reconciliada, esta afirmação deixa de valer junto.
    /// </para>
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
        Inicio = EmUtc(cronograma.Inicio);
        Fim = EmUtc(cronograma.Fim);
        FaseConcluinteCodigo = NormalizarCodigo(cronograma.FaseConcluinteCodigo);
        DefinirUnidadeResponsavel(cronograma.UnidadeResponsavel);
    }

    private void ReconciliarProdutos(IReadOnlyList<ProdutoDaFase> chegados)
    {
        ProdutosReconciliados<ProdutoDaFase> reconciliados = ProdutosPublicados
            .ReconciliarPreservandoIdentidadeMesmoComTrocaDePapel(_produtos, chegados, static ato => ato);

        _produtos.Clear();
        AdotarProdutos(reconciliados.Produtos);
    }

    private void ReporBancasRequeridasDe(FaseCronograma origem)
    {
        List<BancaRequerida> bancasRequeridas = [.. origem.BancasRequeridas];

        _bancasRequeridas.Clear();
        AdotarBancasRequeridas(bancasRequeridas);
    }

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

    private void AdotarProdutos(IEnumerable<ProdutoDaFase> produtos)
    {
        foreach (ProdutoDaFase produto in produtos)
        {
            produto.VincularFase(Id);
            _produtos.Add(produto);
        }
    }

    private void AdotarBancasRequeridas(IEnumerable<BancaRequerida> bancasRequeridas)
    {
        foreach (BancaRequerida banca in bancasRequeridas)
        {
            banca.VincularFase(Id);
            _bancasRequeridas.Add(banca);
        }
    }

    /// <summary>
    /// Fixa a representação da janela em UTC preservando o instante — o cliente informa
    /// um instante inequívoco (RFC 3339), e qual offset ele escolheu para escrevê-lo é
    /// detalhe de transporte, não dado de domínio. Mesma normalização que
    /// <c>AuthorizationRequestContext</c> aplica à data de acesso.
    /// </summary>
    private static DateTimeOffset? EmUtc(DateTimeOffset? instante) => instante?.ToUniversalTime();

    /// <summary>
    /// Texto em branco e ausência são o mesmo estado — "esta fase não aponta concluinte" —,
    /// e deixar os dois entrarem faria a mesma configuração produzir bytes canônicos
    /// distintos conforme o cliente enviasse <c>null</c> ou <c>""</c>.
    /// </summary>
    private static string? NormalizarCodigo(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
}
