namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Linq;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Exigência documental de um <see cref="ProcessoSeletivo"/> declara qual
/// documento é exigido, em que fase, com que aplicabilidade e sob qual gatilho DNF
/// (<see cref="Condicoes"/>). <c>EntityBase</c> puro (sem soft-delete) — o regime
/// append-only/forense pertence a <see cref="VersaoConfiguracao"/>, não à configuração
/// viva; a coleção do agregado é substituível por inteiro
/// (<see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/>), mesmo padrão de
/// <see cref="FaseCronograma"/>/<see cref="ModalidadeSelecionada"/>.
/// </summary>
public sealed class DocumentoExigido : EntityBase, IParteDoProcesso
{
    /// <summary>O tamanho de um código de fato (o mesmo do fato citado por uma condição de gatilho).</summary>
    public const int FatoComprovadoMaxLength = FatoDoCatalogo.CodigoMaxLength;

    public const string FatosComprovadosInvalidos = "DocumentoExigido.FatosComprovadosInvalidos";

    /// <summary>A exigência comprova um fato que nenhum formulário do processo coleta.</summary>
    public const string FatoComprovadoForaDoProcesso = "DocumentoExigido.FatoComprovadoForaDoProcesso";

    public ChaveLocal Chave { get; private set; }

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Fase do cronograma do mesmo processo em que o documento é exigido — NOT NULL.</summary>
    public Guid ExigidoNaFaseId { get; private set; }

    /// <summary>
    /// Etapa dessa mesma fase que coleta o documento, quando a fase se subdivide — nula
    /// quando o documento é da fase inteira, que é o caso de toda fase sem etapa.
    ///
    /// A âncora da exigência continua sendo a fase: é dela que a remoção de fase se
    /// defende e é por ela que o gate de reenvio agrupa. A etapa refina a resposta a "o
    /// que se coleta aqui" — a habilitação do certame regional tem oito etapas, e cada
    /// uma pede o seu comprovante, não os oito.
    /// </summary>
    public Guid? ExigidoNaEtapaId { get; private set; }

    /// <summary>
    /// O formulário a que o documento pertence, quando a fase coleta formulário: é no bloco de
    /// comprovação dele que o candidato o apresenta. A fase diz o momento da coleta e a finalidade
    /// diz o formulário, porque inscrição e isenção podem dividir a mesma fase. Nula só em fase que
    /// não coleta formulário nenhum. Declarada por quem configura, nunca deduzida da fase.
    /// </summary>
    public FinalidadeFormulario? Finalidade { get; private set; }

    /// <summary>O código do formulário a que o documento pertence; nulo só em fase que não coleta formulário nenhum.</summary>
    public CodigoDoFormulario? FormularioCodigo { get; private set; }

    /// <summary>Id do <c>TipoDocumento</c> vivo no momento da configuração — snapshot-copy (ADR-0061), sem FK cross-módulo.</summary>
    public Guid TipoDocumentoOrigemId { get; private set; }

    /// <summary>Código classificatório congelado — snapshot-copy.</summary>
    public string TipoDocumentoCodigo { get; private set; } = string.Empty;

    /// <summary>Rótulo legível congelado — snapshot-copy.</summary>
    public string TipoDocumentoNome { get; private set; } = string.Empty;

    /// <summary>Categoria classificatória congelada — snapshot-copy.</summary>
    public string TipoDocumentoCategoria { get; private set; } = string.Empty;

    /// <summary>
    /// Classificação de proteção do tipo de documento, congelada na cópia. É o piso da proteção
    /// do arquivo que responde à exigência, e nunca fica abaixo da dos fatos que a exigência
    /// comprova.
    /// </summary>
    public ClassificacaoDeProtecao ClassificacaoProtecao { get; private set; }

    /// <summary>Finalidade do tratamento do tipo de documento, congelada na cópia.</summary>
    public string FinalidadeTratamento { get; private set; } = string.Empty;

    /// <summary>Hipótese legal do tratamento do tipo de documento (token canônico), congelada na cópia.</summary>
    public string HipoteseLegal { get; private set; } = string.Empty;

    /// <summary>
    /// Códigos dos fatos do próprio processo que o documento comprova. Vazia quando a exigência não
    /// declara fato comprovado. O fato que só aciona a exigência pelo gatilho não entra aqui.
    /// </summary>
    public string[] FatosComprovados { get; private set; } = [];

    public Aplicabilidade Aplicabilidade { get; private set; }

    public bool Obrigatorio { get; private set; }

    public bool AbrePrazoParaReenvio { get; private set; }

    /// <summary>
    /// Consequência do descumprimento, quando declarada
    /// (ADR-0074). Campo de transporte — a coerência com <c>ModalidadeSelecionada.AcaoQuandoIndeferido</c>
    /// é validada na conformidade legal (CA-05), sem duplicar campo.
    /// </summary>
    public ConsequenciaDoDescumprimento? ConsequenciaDoDescumprimento { get; private set; }

    public RequisitosDoDocumento Requisitos { get; private set; } = null!;
    public PrazoDeEmissao? PrazoDeEmissao => Requisitos.PrazoDeEmissao;
    public ValidadeDoDocumento? Validade => Requisitos.Validade;
    public FormatosPermitidos FormatosPermitidos => Requisitos.FormatosPermitidos;
    public int? TamanhoMaximoBytes => Requisitos.TamanhoMaximoBytes;
    public ModeloDaExigencia? Modelo => Requisitos.Modelo;

    private readonly List<CondicaoGatilho> _condicoes = [];

    /// <summary>Gatilho DNF — vazia significa "sem gatilho": GERAL é sempre exigida, CONDICIONAL vazia é exigida de ninguém.</summary>
    public IReadOnlyCollection<CondicaoGatilho> Condicoes => _condicoes.AsReadOnly();

    private readonly List<DocumentoExigidoBaseLegal> _basesLegais = [];

    /// <summary>Base legal 1:N (ADR-0074) — embasamento rastreável e auditável da exigência; validada no gate de publicação, não na escrita.</summary>
    public IReadOnlyCollection<DocumentoExigidoBaseLegal> BasesLegais => _basesLegais.AsReadOnly();

    private DocumentoExigido() { }

    public static List<FieldError> ValidarFormaBasica(
        Aplicabilidade? aplicabilidade, string? consequenciaDoDescumprimento, int quantidadeCondicoes,
        FinalidadeFormulario? finalidade = null, IReadOnlyList<string>? fatosComprovados = null)
    {
        List<FieldError> erros = finalidade is { } declarada ? FormaDoCabecalho.ValidarFinalidade(declarada) : [];
        ValidarAplicabilidade(aplicabilidade, quantidadeCondicoes, erros);
        ValidarConsequencia(consequenciaDoDescumprimento, erros);
        ValidarFatos(fatosComprovados, erros);
        return erros;
    }

    private static void ValidarAplicabilidade(Aplicabilidade? aplicabilidade, int quantidadeCondicoes, List<FieldError> erros)
    {
        if (aplicabilidade is null || !Enum.IsDefined(aplicabilidade.Value))
        {
            erros.Add(new("aplicabilidade", new DomainError("DocumentoExigido.AplicabilidadeObrigatoria",
                "A aplicabilidade da exigência documental é obrigatória e deve ser GERAL ou CONDICIONAL.")));
        }

        if (aplicabilidade == Aplicabilidade.Geral && quantidadeCondicoes > 0)
        {
            erros.Add(new("condicoes", new DomainError("DocumentoExigido.GeralComCondicao",
                "A exigência é geral e não pode ter condição de aplicação ativa.")));
        }
    }

    private static void ValidarConsequencia(string? consequenciaDoDescumprimento, List<FieldError> erros)
    {
        string? normalizada = NormalizarConsequencia(consequenciaDoDescumprimento);
        if (normalizada is not null && !CatalogoDe<ConsequenciaDoDescumprimento>.TentarObter(normalizada, out _))
        {
            erros.Add(new("consequenciaDoDescumprimento", new DomainError("DocumentoExigido.ConsequenciaDoDescumprimentoInvalida",
                "A consequência do descumprimento da exigência não é uma das consequências cadastradas.")));
        }
    }

    private static void ValidarFatos(IReadOnlyList<string>? fatos, List<FieldError> erros)
    {
        if (fatos is { Count: > 0 }
            && (fatos.Any(static f => string.IsNullOrWhiteSpace(f) || f.Length > FatoComprovadoMaxLength)
                || fatos.Distinct(StringComparer.Ordinal).Count() != fatos.Count))
        {
            erros.Add(new("fatosComprovados", new DomainError(FatosComprovadosInvalidos,
                $"Os fatos comprovados são códigos de fato não repetidos, de até {FatoComprovadoMaxLength} caracteres.")));
        }
    }

    /// <summary>
    /// Normaliza espaços-em-branco para <see langword="null"/> ANTES de validar — sem isso,
    /// " " escapa da checagem de domínio (não é "presente" o bastante para reprovar, nem
    /// null o bastante para <see cref="DeterminaResultado"/> ignorar) e é persistido cru.
    /// </summary>
    private static string? NormalizarConsequencia(string? consequenciaDoDescumprimento) =>
        string.IsNullOrWhiteSpace(consequenciaDoDescumprimento) ? null : consequenciaDoDescumprimento;

    private static ConsequenciaDoDescumprimento? ObterConsequencia(string? codigo) =>
        CatalogoDe<ConsequenciaDoDescumprimento>.TentarObter(codigo, out ConsequenciaDoDescumprimento valor) ? valor : null;

    public static Result<DocumentoExigido> Criar(ChaveLocal chave, DocumentoExigidoDeclarado declarado)
    {
        ExigirPartes(declarado);
        ExigirIdentificadores(declarado.Tipo, declarado.Momento);
        List<FieldError> erros = ValidarFormaBasica(declarado.Aplicabilidade.Tipo, declarado.Indeferimento.ConsequenciaDoDescumprimento,
            declarado.Aplicabilidade.Condicoes.Count, declarado.Momento.Finalidade, declarado.FatosComprovados);
        erros.AddRange(RequisitosDoDocumento.ValidarTamanho(declarado.Requisitos.TamanhoMaximoBytes));
        if (erros.Count > 0)
        {
            return Result<DocumentoExigido>.ValidationFailure(erros);
        }

        DocumentoExigido documento = new(chave, declarado);
        documento.VincularPartes(declarado.Aplicabilidade, declarado.Indeferimento);
        return Result<DocumentoExigido>.Success(documento);
    }

    public static DocumentoExigido Reidratar(ChaveLocal chave, Guid id, DocumentoExigidoDeclarado declarado)
    {
        ExigirPartes(declarado);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A exigência reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        DocumentoExigido documento = new(chave, declarado) { Id = id };
        documento.ConsequenciaDoDescumprimento = ObterConsequencia(declarado.Indeferimento.ConsequenciaDoDescumprimento);
        documento.VincularPartes(declarado.Aplicabilidade, declarado.Indeferimento);
        return documento;
    }

    private DocumentoExigido(ChaveLocal chave, DocumentoExigidoDeclarado declarado)
    {
        Chave = chave;
        ExigidoNaFaseId = declarado.Momento.FaseId;
        ExigidoNaEtapaId = declarado.Momento.EtapaId;
        FormularioCodigo = declarado.Momento.Formulario;
        Finalidade = declarado.Momento.Finalidade;
        CopiarTipo(declarado.Tipo);
        FatosComprovados = [.. declarado.FatosComprovados ?? []];
        Aplicabilidade = declarado.Aplicabilidade.Tipo
            ?? throw new ArgumentException("A exigência deve declarar a aplicabilidade.", nameof(declarado));
        Obrigatorio = declarado.Indeferimento.Obrigatorio;
        AbrePrazoParaReenvio = declarado.Indeferimento.AbrePrazoParaReenvio;
        ConsequenciaDoDescumprimento = ObterConsequencia(declarado.Indeferimento.ConsequenciaDoDescumprimento);
        Requisitos = declarado.Requisitos;
    }

    private void CopiarTipo(CopiaDoTipoDeDocumento tipo)
    {
        TipoDocumentoOrigemId = tipo.Id;
        TipoDocumentoCodigo = tipo.Codigo;
        TipoDocumentoNome = tipo.Nome.Trim();
        TipoDocumentoCategoria = tipo.Categoria.Trim();
        RecongelarProtecao(tipo.Protecao);
    }

    private void VincularPartes(AplicabilidadeDaExigencia aplicabilidade, IndeferimentoDoDocumento indeferimento)
    {
        foreach (CondicaoGatilho condicao in aplicabilidade.Condicoes)
        {
            condicao.VincularDocumentoExigido(Id);
            _condicoes.Add(condicao);
        }

        foreach (DocumentoExigidoBaseLegal baseLegal in indeferimento.BasesLegais)
        {
            baseLegal.VincularDocumentoExigido(Id);
            _basesLegais.Add(baseLegal);
        }
    }

    private static void ExigirPartes(DocumentoExigidoDeclarado declarado)
    {
        ArgumentNullException.ThrowIfNull(declarado);
        ArgumentNullException.ThrowIfNull(declarado.Tipo, nameof(declarado));
        ArgumentNullException.ThrowIfNull(declarado.Momento, nameof(declarado));
        ArgumentNullException.ThrowIfNull(declarado.Aplicabilidade, nameof(declarado));
        ArgumentNullException.ThrowIfNull(declarado.Indeferimento, nameof(declarado));
        ArgumentNullException.ThrowIfNull(declarado.Requisitos, nameof(declarado));
        ArgumentNullException.ThrowIfNull(declarado.Aplicabilidade.Condicoes, nameof(declarado));
        ArgumentNullException.ThrowIfNull(declarado.Indeferimento.BasesLegais, nameof(declarado));
        ExigirTipo(declarado.Tipo);
    }

    private static void ExigirIdentificadores(CopiaDoTipoDeDocumento tipo, MomentoDaExigencia momento)
    {
        if (momento.FaseId == Guid.Empty || tipo.Id == Guid.Empty)
        {
            throw new ArgumentException("A fase e o tipo do documento são obrigatórios.", nameof(momento));
        }
    }

    private static void ExigirTipo(CopiaDoTipoDeDocumento tipo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo.Codigo, nameof(tipo));
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo.Nome, nameof(tipo));
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo.Categoria, nameof(tipo));
        ExigirProtecao(tipo.Protecao);
    }

    /// <summary>
    /// A proteção vem de um tipo de documento que a Configuração já validou: as guardas aqui são a
    /// última linha contra erro de programação, não revalidação de negócio.
    /// </summary>
    private static void ExigirProtecao(ProtecaoDoDocumento protecao)
    {
        ArgumentNullException.ThrowIfNull(protecao);
        if (protecao.Violacao() is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(protecao));
        }
    }

    /// <summary>
    /// Troca a proteção copiada pela vigente no tipo de documento. A exigência copia a proteção ao ser
    /// gravada, e o tipo pode ser reclassificado até a publicação: é o valor vigente na publicação ou
    /// na retificação que se congela.
    /// </summary>
    internal void RecongelarProtecao(ProtecaoDoDocumento protecao)
    {
        ExigirProtecao(protecao);
        ClassificacaoProtecao = protecao.Classificacao;
        FinalidadeTratamento = protecao.Finalidade.Trim();
        HipoteseLegal = protecao.HipoteseLegal.Trim();
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Determina se a exigência "conta" para efeito de resultado — obrigatória ou com
    /// qualquer consequência de indeferimento declarada. Usado pela trava CA-01 (<c>CONDICIONAL</c> vazia que determina resultado bloqueia
    /// publicação) e pelo gate de base legal.
    /// </summary>
    public bool DeterminaResultado() => Obrigatorio || ConsequenciaDoDescumprimento is not null;

    /// <summary>
    /// Projeção "somente <see cref="StatusBaseLegal.Resolvido"/>" (CA-06)
    /// — o que a conformidade legal materializa no bloco congelado; bases
    /// <see cref="StatusBaseLegal.Pendente"/> são rascunho e nunca aparecem aqui.
    /// </summary>
    public IEnumerable<DocumentoExigidoBaseLegal> BasesLegaisResolvidas() =>
        _basesLegais.Where(static b => b.Situacao == StatusBaseLegal.Resolvido);

    /// <summary>
    /// A exigência se aplica a um candidato com estes fatos? GERAL sempre é
    /// <see cref="Ternario.Verdadeiro"/> — o gatilho nunca é avaliado. CONDICIONAL depende
    /// do predicado DNF ternário: zero cláusulas vivas nunca casa com
    /// ninguém (<see cref="Ternario.Falso"/>, estado estrutural — CA-01 "exigida de
    /// ninguém"), e um fato citado que não está resolvido para este candidato produz
    /// <see cref="Ternario.Indeterminado"/>, nunca <see cref="Ternario.Falso"/> (fail-closed).
    /// Usado tanto pelo resolvedor de exigências documentais (por candidato real)
    /// quanto pelo gate de conformidade legal (<see cref="Services.AvaliadorConformidadeLegal"/>
    /// — só com os fatos cujos valores são modalidades, para provar cobertura incondicional
    /// de uma modalidade inteira).
    /// </summary>
    public Ternario AplicavelPara(IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);

        if (Aplicabilidade == Aplicabilidade.Geral)
        {
            return Ternario.Verdadeiro;
        }

        return _condicoes.Count == 0 ? Ternario.Falso : AvaliarCondicoes(fatosResolvidos);
    }

    private Ternario AvaliarCondicoes(IReadOnlyDictionary<string, FatoResolvido> fatosResolvidos)
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. _condicoes.Select(static c => (c.Clausula, c.ParaCondicaoDnf()))]).Value!;
        return predicado.Avaliar(fatosResolvidos);
    }

    public bool PodeAlcancarModalidade(string modalidadeCodigo, FatosDeModalidade fatosDeModalidade) =>
        AlcanceDaExigenciaPorModalidade.PodeAlcancar(Aplicabilidade, Condicoes, modalidadeCodigo, fatosDeModalidade);

    /// <summary>
    /// Coerência entre <see cref="Aplicabilidade"/> e a existência de condição de gatilho
    /// viva (CA-01, ADR-0071): <c>GERAL</c> nunca convive com condição viva.
    /// Chamado por <see cref="Criar"/> contra a coleção real de <see cref="Condicoes"/>.
    /// </summary>
    public DomainError? GarantirCoerenciaAplicabilidade(bool possuiCondicaoViva)
    {
        if (Aplicabilidade == Aplicabilidade.Geral && possuiCondicaoViva)
        {
            return new DomainError(
                "DocumentoExigido.GeralComCondicao",
                $"A exigência '{TipoDocumentoCodigo}' é geral e não pode ter condição de aplicação ativa.");
        }

        return null;
    }
}
