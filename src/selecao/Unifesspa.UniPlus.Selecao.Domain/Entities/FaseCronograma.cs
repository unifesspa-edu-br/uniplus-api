namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Uma fase do cronograma de um <see cref="ProcessoSeletivo"/> (1..*, Story #851) — o
/// eixo <b>temporal</b> do certame (janela, ordem, dono institucional, origem da data,
/// ato produzido), distinto do eixo de <b>pontuação</b> (<see cref="EtapaProcesso"/>).
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
/// <b>Invariantes que esta factory prova sozinha</b> (não dependem de leitura externa):
/// janela obrigatória/opcional conforme <see cref="OrigemData"/> (CA-07), janela não
/// invertida, e as duas primeiras invariantes de <see cref="RegraRecursoFase"/> que
/// dependem da fase-mãe (produz resultado, não é resultado definitivo, a âncora é o
/// próprio ato produzido — itens 1/2 do §3.6). A resolução do ato âncora contra o
/// catálogo de Publicações (existe, vigente, não congelante) e da regra contra o
/// <c>rol_de_regras</c> são I/O — Application, ADR-0042.
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

    /// <summary>Se a fase produz resultado — decide o piso mínimo havendo vagas (§3.4) e é pré-condição de recurso.</summary>
    public bool ProduzResultado { get; private set; }

    /// <summary>Se o resultado produzido é definitivo — não cabe recurso contra ele (CA-16).</summary>
    public bool ResultadoDefinitivo { get; private set; }

    /// <summary>Se a fase coleta inscrição — decide o piso mínimo quando <see cref="OrigemCandidatos.InscricaoPropria"/>.</summary>
    public bool ColetaInscricao { get; private set; }

    public DateTimeOffset? Inicio { get; private set; }

    public DateTimeOffset? Fim { get; private set; }

    /// <summary>Código do tipo de ato que esta fase produz — a âncora de <see cref="RegraRecurso"/> é sempre este.</summary>
    public string? AtoProduzidoCodigo { get; private set; }

    /// <summary>Snapshot-copy de <c>TipoAtoPublicado.EfeitoIrreversivel</c> do ato produzido.</summary>
    public bool AtoProduzidoEfeitoIrreversivel { get; private set; }

    /// <summary>Presença = a fase admite recurso (0..1, §3.6).</summary>
    public RegraRecursoFase? RegraRecurso { get; private set; }

    private readonly List<BancaRequerida> _bancasRequeridas = [];
    public IReadOnlyCollection<BancaRequerida> BancasRequeridas => _bancasRequeridas.AsReadOnly();

    private FaseCronograma() { }

    /// <summary>
    /// Cria uma fase do cronograma, validando as invariantes locais (janela ×
    /// <see cref="OrigemData"/>, ato produzido × regra de recurso). A unicidade de
    /// <see cref="Ordem"/> e de <see cref="FaseCanonicaOrigemId"/> dentro do cronograma, a
    /// precedência entre fases e a bicondicional fase×etapa são validadas pela raiz
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
        bool produzResultado,
        bool resultadoDefinitivo,
        bool coletaInscricao,
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        string? atoProduzidoCodigo,
        bool atoProduzidoEfeitoIrreversivel,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        RegraRecursoFase? regraRecurso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(donoInstitucional);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);

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

        // Resultado definitivo pressupõe que a fase produza resultado — mesma invariante
        // já vigente no cadastro (FaseCanonica.Criar); repetida aqui porque o snapshot é
        // copiado por valor e pode, em tese, ser reidratado de um envelope adulterado.
        if (resultadoDefinitivo && !produzResultado)
        {
            throw new ArgumentException(
                "Resultado definitivo pressupõe que a fase produza resultado.", nameof(resultadoDefinitivo));
        }

        // JanelaInvertida vale independentemente da origem da data — uma fase DELEGADA
        // com janela declarada não escapa da checagem de coerência interna.
        if (inicio is { } inicioValor && fim is { } fimValor && fimValor < inicioValor)
        {
            return Result<FaseCronograma>.Failure(new DomainError(
                "FaseCronograma.JanelaInvertida",
                $"A fase '{codigo}' tem o fim da janela ({fimValor:O}) antes do início ({inicioValor:O})."));
        }

        // CA-07: OrigemData=PROPRIA exige janela; DELEGADA aceita "sem data" como estado
        // válido — o setor responsável não congela data que não controla (§3.2).
        if (origemData == OrigemDataFase.Propria && (inicio is null || fim is null))
        {
            return Result<FaseCronograma>.Failure(new DomainError(
                "FaseCronograma.JanelaObrigatoriaEmDataPropria",
                $"A fase '{codigo}' tem origem de data própria e exige início e fim da janela."));
        }

        // Uma fase que produz resultado precisa declarar QUAL ato ela produz — é o que
        // RegraRecursoFase ancora (item 2 do §3.6), e é dado congelado independentemente
        // de haver recurso ou não (§3.7).
        if (produzResultado && string.IsNullOrWhiteSpace(atoProduzidoCodigo))
        {
            return Result<FaseCronograma>.Failure(new DomainError(
                "FaseCronograma.AtoProduzidoObrigatorio",
                $"A fase '{codigo}' produz resultado e precisa declarar o código do ato que produz."));
        }

        if (regraRecurso is not null)
        {
            // Item 1 do §3.6: recurso só cabe onde há resultado, e nunca contra resultado
            // definitivo.
            if (!produzResultado)
            {
                return Result<FaseCronograma>.Failure(new DomainError(
                    "RegraRecursoFase.FaseNaoProduzResultado",
                    $"A fase '{codigo}' não produz resultado e não pode admitir regra de recurso."));
            }

            if (resultadoDefinitivo)
            {
                return Result<FaseCronograma>.Failure(new DomainError(
                    "RegraRecursoFase.RecursoContraResultadoDefinitivo",
                    $"A fase '{codigo}' produz resultado definitivo — não cabe recurso contra ele."));
            }

            // Item 2 do §3.6: o ato recorrido é SEMPRE o ato da própria fase — ancorar no
            // ato de outra fase é recusado.
            if (!string.Equals(regraRecurso.Args.AtoAncoraCodigo, atoProduzidoCodigo, StringComparison.Ordinal))
            {
                return Result<FaseCronograma>.Failure(new DomainError(
                    "RegraRecursoFase.AncoraDeOutraFase",
                    $"A fase '{codigo}' produz o ato '{atoProduzidoCodigo}', mas a regra de recurso ancora em '{regraRecurso.Args.AtoAncoraCodigo}' — o ato recorrido tem de ser o da própria fase."));
            }
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
            ProduzResultado = produzResultado,
            ResultadoDefinitivo = resultadoDefinitivo,
            ColetaInscricao = coletaInscricao,
            Inicio = inicio,
            Fim = fim,
            AtoProduzidoCodigo = atoProduzidoCodigo,
            AtoProduzidoEfeitoIrreversivel = atoProduzidoEfeitoIrreversivel,
        };

        foreach (BancaRequerida banca in bancasRequeridas)
        {
            banca.VincularFase(fase.Id);
            fase._bancasRequeridas.Add(banca);
        }

        if (regraRecurso is not null)
        {
            regraRecurso.VincularFase(fase.Id);
            fase.RegraRecurso = regraRecurso;
        }

        return Result<FaseCronograma>.Success(fase);
    }

    /// <summary>
    /// Reidrata uma fase a partir de uma <see cref="VersaoConfiguracao"/> congelada,
    /// <b>preservando o <see cref="EntityBase.Id"/></b> — Story #554 (PR #903, bump 1.2),
    /// achado de revisão: duas referências cruzadas do envelope 1.2 apontam para
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
        bool produzResultado,
        bool resultadoDefinitivo,
        bool coletaInscricao,
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        string? atoProduzidoCodigo,
        bool atoProduzidoEfeitoIrreversivel,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        RegraRecursoFase? regraRecurso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(donoInstitucional);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A fase reidratada deve declarar o Id congelado no envelope.", nameof(id));
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
            ProduzResultado = produzResultado,
            ResultadoDefinitivo = resultadoDefinitivo,
            ColetaInscricao = coletaInscricao,
            Inicio = inicio,
            Fim = fim,
            AtoProduzidoCodigo = atoProduzidoCodigo,
            AtoProduzidoEfeitoIrreversivel = atoProduzidoEfeitoIrreversivel,
        };

        foreach (BancaRequerida banca in bancasRequeridas)
        {
            banca.VincularFase(fase.Id);
            fase._bancasRequeridas.Add(banca);
        }

        if (regraRecurso is not null)
        {
            regraRecurso.VincularFase(fase.Id);
            fase.RegraRecurso = regraRecurso;
        }

        return fase;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Atualiza os dados da MESMA fase (mesmo <see cref="EntityBase.Id"/>) em vez de
    /// recriá-la — usado tanto pela reposição da configuração congelada
    /// (<see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>, reconciliação por
    /// <see cref="Ordem"/> — a versão 1.1 do envelope nunca congelou o <c>Id</c> (§3.7), e
    /// mesmo na 1.2, que passou a congelá-lo (<see cref="Reidratar"/>, Story #554, PR #903),
    /// a reconciliação aqui continua por Ordem: é a instância VIVA rastreada que precisa
    /// sobreviver — a do EF, não a decodificada) quanto pela redefinição ao vivo do
    /// cronograma (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>, reconciliação por
    /// <see cref="FaseCanonicaOrigemId"/> — a identidade estável de uma fase; aqui
    /// <paramref name="ordem"/> PODE mudar, ao contrário do caminho de restauração).
    /// </summary>
    /// <remarks>
    /// Sem esta reconciliação, repor o cronograma faria <c>Clear()</c> + <c>Add</c> de
    /// instâncias novas — DELETE das fases antigas e INSERT das novas na MESMA
    /// transação. Quando a nova fase reocupa a MESMA <see cref="Ordem"/> da antiga (o
    /// caso comum: a sessão editorial só mudou datas, não renumerou o cronograma),
    /// <c>ux_fases_cronograma_processo_ordem</c> não tem como saber que o DELETE
    /// "libera" o valor antes do INSERT — o EF Core não infere essa ordem entre
    /// entidades sem relação de FK, e o SaveChanges pode colidir na constraint.
    /// Reconciliar em vez de recriar evita o DELETE+INSERT do mesmo slot.
    /// </remarks>
    internal void AtualizarSnapshot(
        Guid faseCanonicaOrigemId,
        int ordem,
        string codigo,
        string donoInstitucional,
        OrigemDataFase origemData,
        bool agrupaEtapas,
        bool permiteComplementacao,
        bool produzResultado,
        bool resultadoDefinitivo,
        bool coletaInscricao,
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        string? atoProduzidoCodigo,
        bool atoProduzidoEfeitoIrreversivel,
        IReadOnlyList<BancaRequerida> bancasRequeridas,
        RegraRecursoFase? regraRecurso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(donoInstitucional);
        ArgumentNullException.ThrowIfNull(bancasRequeridas);

        FaseCanonicaOrigemId = faseCanonicaOrigemId;
        Ordem = ordem;
        Codigo = codigo.Trim();
        DonoInstitucional = donoInstitucional.Trim();
        OrigemData = origemData;
        AgrupaEtapas = agrupaEtapas;
        PermiteComplementacao = permiteComplementacao;
        ProduzResultado = produzResultado;
        ResultadoDefinitivo = resultadoDefinitivo;
        ColetaInscricao = coletaInscricao;
        Inicio = inicio;
        Fim = fim;
        AtoProduzidoCodigo = atoProduzidoCodigo;
        AtoProduzidoEfeitoIrreversivel = atoProduzidoEfeitoIrreversivel;

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
}
