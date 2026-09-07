namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Uma fase do cronograma de um <see cref="ProcessoSeletivo"/> (1..*, Story #851) — o
/// eixo <b>temporal</b> do certame (janela, ordem, dono institucional, o que a fase
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
public sealed class FaseCronograma : EntityBase
{
    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Posição do certame na linha do tempo — única dentro do cronograma (CA-06).</summary>
    public int Ordem { get; private set; }

    /// <summary>Id (Guid v7) da <c>FaseCanonica</c> viva de origem, no momento do congelamento.</summary>
    public Guid FaseCanonicaOrigemId { get; private set; }

    /// <summary>Código canônico congelado (ex.: <c>"HOMOLOGACAO"</c>) — snapshot-copy.</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Dono institucional congelado (token) — snapshot-copy estrito (§3.2); o processo não o sobrescreve.</summary>
    public string DonoInstitucional { get; private set; } = string.Empty;

    /// <summary>Quem controla a data desta fase — snapshot-copy; decide a obrigatoriedade da janela (CA-07).</summary>
    public OrigemDataFase OrigemData { get; private set; }

    /// <summary>Se esta fase agrupa etapas pontuadas — bicondicional com a existência de etapa (§3.5).</summary>
    public bool AgrupaEtapas { get; private set; }

    public bool PermiteComplementacao { get; private set; }

    /// <summary>Se a fase coleta inscrição — decide o piso mínimo quando <see cref="OrigemCandidatos.InscricaoPropria"/>.</summary>
    public bool ColetaInscricao { get; private set; }

    /// <summary>Verdadeiro quando a fase abre a janela de pedido de isenção da taxa.</summary>
    public bool ColetaSolicitacaoIsencao { get; private set; }

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
    public static Result<FaseCronograma> Criar(
        int ordem,
        Guid faseCanonicaOrigemId,
        string codigo,
        string donoInstitucional,
        OrigemDataFase origemData,
        bool agrupaEtapas,
        bool permiteComplementacao,
        bool coletaInscricao,
        bool coletaSolicitacaoIsencao,
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo,
        bool emiteParecerIndividual,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        RegraRecursoFase? regraRecurso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(donoInstitucional);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);

        // A janela é fixada em UTC ANTES das validações, para que a mensagem de
        // JanelaInvertida e o valor gravado falem da mesma representação do instante.
        inicio = EmUtc(inicio);
        fim = EmUtc(fim);

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

        // Acumula (ADR-0125) as checagens abaixo — todas independentes entre si, nenhuma
        // decide o que checar a partir do resultado de outra. Diferente das demais fatias
        // desta rolagem, NÃO há uma "ValidarFormaBasica" pré-I/O extraída para o handler
        // chamar numa primeira passada: origemData é snapshot-copy do CADASTRO
        // (FaseCanonica), resolvido via IFaseCanonicaReader — não primitivo do payload do
        // cliente —, e os produtos só existem depois de o catálogo de tipos de ato ter
        // sido consultado. Ordem permanece só throw (defesa de programação, nunca
        // acumulada) e continua coberta pelo FluentValidation; a janela (Fim >= Início) NÃO
        // tem mais regra equivalente no validator — ela é a acumulada abaixo
        // (JanelaInvertida), e deixá-la também no validator faria o middleware bloquear
        // sozinho, sem nunca deixar o payload chegar aqui para acumular junto de outras
        // violações da mesma fase.
        List<FieldError> erros = [];

        // JanelaInvertida vale independentemente da origem da data — uma fase DELEGADA
        // com janela declarada não escapa da checagem de coerência interna.
        if (inicio is { } inicioValor && fim is { } fimValor && fimValor < inicioValor)
        {
            erros.Add(new("fim", new DomainError(
                "FaseCronograma.JanelaInvertida",
                $"A fase '{codigo}' tem o fim da janela ({fimValor:O}) antes do início ({inicioValor:O}).")));
        }

        // CA-07: OrigemData=PROPRIA exige janela; DELEGADA aceita "sem data" como estado
        // válido — o setor responsável não congela data que não controla (§3.2).
        if (origemData == OrigemDataFase.Propria && (inicio is null || fim is null))
        {
            erros.Add(new(null, new DomainError(
                "FaseCronograma.JanelaObrigatoriaEmDataPropria",
                $"A fase '{codigo}' tem origem de data própria e exige início e fim da janela.")));
        }

        // O código do ato é a chave natural do produto dentro da fase, e o índice único da
        // tabela a espelha. Declarar o mesmo ato duas vezes daria à fase duas intenções
        // sobre a mesma publicação — possivelmente com papéis divergentes —, e o motor não
        // teria como eleger uma. Publicar preliminar e definitiva da mesma matéria não cai
        // aqui: são atos com códigos distintos no catálogo.
        string? atoDuplicado = produtos
            .GroupBy(static p => p.AtoCodigo, StringComparer.Ordinal)
            .FirstOrDefault(static g => g.Count() > 1)?.Key;
        if (atoDuplicado is not null)
        {
            erros.Add(new("produtos", new DomainError(
                "FaseCronograma.AtoDuplicadoNaFase",
                $"A fase '{codigo}' declara o ato '{atoDuplicado}' mais de uma vez — cada tipo de ato é declarado uma única vez por fase.")));
        }

        bool produzResultado = produtos.Any(static p => p.Papel is not null);

        // O parecer individual é o que dá ao candidato fundamento claro para recorrer. Sem
        // publicação de resultado não há decisão a contestar nem prazo a correr, e a
        // promessa ficaria sem objeto.
        if (emiteParecerIndividual && !produzResultado)
        {
            erros.Add(new("emiteParecerIndividual", new DomainError(
                "FaseCronograma.ParecerIndividualSemResultado",
                $"A fase '{codigo}' promete parecer individual e não publica nenhum resultado — não haveria decisão a fundamentar.")));
        }

        if (regraRecurso is not null)
        {
            List<ProdutoDaFase> preliminares =
                [.. produtos.Where(static p => p.Papel == PapelProdutoFase.Preliminar)];

            // Recurso é contra o resultado preliminar: é ele que abre a janela de
            // interposição, e é do instante em que é publicado que o prazo corre
            // (UNI-REQ-0115). Fase que não publica nenhum não tem decisão a contestar — nem
            // a que só publica o definitivo, que encerra a matéria em vez de abri-la.
            if (preliminares.Count == 0)
            {
                erros.Add(new("produtos", new DomainError(
                    "RegraRecursoFase.FaseSemProdutoPreliminar",
                    $"A fase '{codigo}' admite recurso e não publica nenhum produto com papel preliminar — declare o resultado preliminar em que o prazo ancora.")));
            }

            // A âncora é a identidade de um produto DESTA fase, e não um código de tipo de
            // ato: duas fases podem publicar o mesmo tipo, e só a identidade da linha diz de
            // qual das duas publicações o prazo conta. Condicionada à existência de algum
            // preliminar — sem nenhum, já há o erro mais fundamental acima, e esta recusa
            // mandaria escolher entre um conjunto vazio.
            else if (!preliminares.Exists(p => p.Id == regraRecurso.ProdutoAncoraId))
            {
                erros.Add(new("regraRecurso.atoAncoraCodigo", new DomainError(
                    "RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase",
                    $"A regra de recurso da fase '{codigo}' não ancora em nenhum dos produtos preliminares que ela publica ({string.Join(", ", preliminares.Select(static p => p.AtoCodigo).Order(StringComparer.Ordinal))}).")));
            }
        }

        if (erros.Count > 0)
        {
            return Result<FaseCronograma>.ValidationFailure(erros);
        }

        FaseCronograma fase = new()
        {
            Ordem = ordem,
            FaseCanonicaOrigemId = faseCanonicaOrigemId,
            Codigo = codigo.Trim(),
            DonoInstitucional = donoInstitucional.Trim(),
            OrigemData = origemData,
            AgrupaEtapas = agrupaEtapas,
            PermiteComplementacao = permiteComplementacao,
            ColetaInscricao = coletaInscricao,
            ColetaSolicitacaoIsencao = coletaSolicitacaoIsencao,
            Inicio = inicio,
            Fim = fim,
            FaseConcluinteCodigo = NormalizarCodigo(faseConcluinteCodigo),
            EmiteParecerIndividual = emiteParecerIndividual,
        };

        fase.AdotarFilhas(produtos, bancasRequeridas, regraRecurso);

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
        int ordem,
        Guid faseCanonicaOrigemId,
        string codigo,
        string donoInstitucional,
        OrigemDataFase origemData,
        bool agrupaEtapas,
        bool permiteComplementacao,
        bool coletaInscricao,
        bool coletaSolicitacaoIsencao,
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo,
        bool emiteParecerIndividual,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        RegraRecursoFase? regraRecurso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(donoInstitucional);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A fase reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        // A âncora congelada é reposta como estava — é ela que o ato já publicado resolve de
        // volta (UNI-REQ-0093). Conferir que aponta para um produto desta fase é a última
        // linha contra envelope incoerente, e é o que torna a leitura do campo
        // não-tautológica no round-trip.
        if (regraRecurso is not null
            && !produtos.Any(p => p.Id == regraRecurso.ProdutoAncoraId))
        {
            throw new ArgumentException(
                $"A fase '{codigo}' reidratada tem regra de recurso ancorada no produto {regraRecurso.ProdutoAncoraId}, que não está entre os produtos congelados da fase.",
                nameof(regraRecurso));
        }

        FaseCronograma fase = new()
        {
            Id = id,
            Ordem = ordem,
            FaseCanonicaOrigemId = faseCanonicaOrigemId,
            Codigo = codigo.Trim(),
            DonoInstitucional = donoInstitucional.Trim(),
            OrigemData = origemData,
            AgrupaEtapas = agrupaEtapas,
            PermiteComplementacao = permiteComplementacao,
            ColetaInscricao = coletaInscricao,
            ColetaSolicitacaoIsencao = coletaSolicitacaoIsencao,
            Inicio = EmUtc(inicio),
            Fim = EmUtc(fim),
            FaseConcluinteCodigo = NormalizarCodigo(faseConcluinteCodigo),
            EmiteParecerIndividual = emiteParecerIndividual,
        };

        fase.AdotarFilhas(produtos, bancasRequeridas, regraRecurso);

        return fase;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Atualiza os dados da MESMA fase (mesmo <see cref="EntityBase.Id"/>) em vez de
    /// recriá-la — usado tanto pela reposição da configuração congelada
    /// (<see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>, reconciliação por
    /// <see cref="Ordem"/> — é a instância VIVA rastreada que precisa sobreviver, a do EF,
    /// não a decodificada) quanto pela redefinição ao vivo do cronograma
    /// (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>, reconciliação por
    /// <see cref="FaseCanonicaOrigemId"/> — a identidade estável de uma fase; aqui
    /// <paramref name="ordem"/> PODE mudar, ao contrário do caminho de restauração).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sem esta reconciliação, repor o cronograma faria <c>Clear()</c> + <c>Add</c> de
    /// instâncias novas — DELETE das fases antigas e INSERT das novas na MESMA
    /// transação. Quando a nova fase reocupa a MESMA <see cref="Ordem"/> da antiga (o
    /// caso comum: a sessão editorial só mudou datas, não renumerou o cronograma),
    /// <c>ux_fases_cronograma_processo_ordem</c> não tem como saber que o DELETE
    /// "libera" o valor antes do INSERT — o EF Core não infere essa ordem entre
    /// entidades sem relação de FK, e o SaveChanges pode colidir na constraint.
    /// Reconciliar em vez de recriar evita o DELETE+INSERT do mesmo slot.
    /// </para>
    /// <para>
    /// <b>Os produtos exigem o mesmo cuidado, e as bancas não.</b>
    /// <c>ux_produtos_da_fase_ato</c> torna <c>(fase, ato_codigo)</c> único, então repor a
    /// coleção por <c>Clear()</c> + <c>Add</c> produziria DELETE+INSERT do mesmo slot na
    /// mesma transação — exatamente a colisão descrita acima. Os produtos são reconciliados
    /// por <see cref="ProdutoDaFase.AtoCodigo"/>, reusando a instância rastreada;
    /// <c>bancas_requeridas</c> não tem índice único e por isso segue com a reposição
    /// simples.
    /// </para>
    /// </remarks>
    internal void AtualizarSnapshot(
        Guid faseCanonicaOrigemId,
        int ordem,
        string codigo,
        string donoInstitucional,
        OrigemDataFase origemData,
        bool agrupaEtapas,
        bool permiteComplementacao,
        bool coletaInscricao,
        bool coletaSolicitacaoIsencao,
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        IReadOnlyList<ProdutoDaFase> produtos,
        string? faseConcluinteCodigo,
        bool emiteParecerIndividual,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        RegraRecursoFase? regraRecurso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(donoInstitucional);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);

        FaseCanonicaOrigemId = faseCanonicaOrigemId;
        Ordem = ordem;
        Codigo = codigo.Trim();
        DonoInstitucional = donoInstitucional.Trim();
        OrigemData = origemData;
        AgrupaEtapas = agrupaEtapas;
        PermiteComplementacao = permiteComplementacao;
        ColetaInscricao = coletaInscricao;
        ColetaSolicitacaoIsencao = coletaSolicitacaoIsencao;
        Inicio = EmUtc(inicio);
        Fim = EmUtc(fim);
        FaseConcluinteCodigo = NormalizarCodigo(faseConcluinteCodigo);
        EmiteParecerIndividual = emiteParecerIndividual;

        List<ProdutoDaFase> reconciliados = [];
        foreach (ProdutoDaFase novo in produtos)
        {
            ProdutoDaFase? rastreado = _produtos
                .Find(p => string.Equals(p.AtoCodigo, novo.AtoCodigo, StringComparison.Ordinal));
            if (rastreado is not null)
            {
                rastreado.AtualizarPapel(novo.Papel);
                reconciliados.Add(rastreado);
            }
            else
            {
                reconciliados.Add(novo);
            }
        }

        // Os que sobraram fora de `reconciliados` saem da coleção e o EF os remove como
        // órfãos; os rastreados voltam para ela e sobrevivem com o Id que já tinham.
        _produtos.Clear();
        foreach (ProdutoDaFase produto in reconciliados)
        {
            produto.VincularFase(Id);
            _produtos.Add(produto);
        }

        _bancasRequeridas.Clear();
        foreach (BancaRequerida banca in bancasRequeridas)
        {
            banca.VincularFase(Id);
            _bancasRequeridas.Add(banca);
        }

        RegraRecurso = regraRecurso;
        if (regraRecurso is not null)
        {
            regraRecurso.VincularFase(Id);
        }
    }

    private void AdotarFilhas(
        IReadOnlyList<ProdutoDaFase> produtos,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        RegraRecursoFase? regraRecurso)
    {
        foreach (ProdutoDaFase produto in produtos)
        {
            produto.VincularFase(Id);
            _produtos.Add(produto);
        }

        foreach (BancaRequerida banca in bancasRequeridas)
        {
            banca.VincularFase(Id);
            _bancasRequeridas.Add(banca);
        }

        if (regraRecurso is not null)
        {
            regraRecurso.VincularFase(Id);
            RegraRecurso = regraRecurso;
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
