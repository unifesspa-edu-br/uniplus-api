namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Linq;
using System.Text.Json;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Exigência documental de um <see cref="ProcessoSeletivo"/> (Story #554): declara qual
/// documento é exigido, em que fase, com que aplicabilidade e sob qual gatilho DNF
/// (<see cref="Condicoes"/>, PR #896). <c>EntityBase</c> puro (sem soft-delete) — o regime
/// append-only/forense pertence a <see cref="VersaoConfiguracao"/>, não à configuração
/// viva; a coleção do agregado é substituível por inteiro
/// (<see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/>), mesmo padrão de
/// <see cref="FaseCronograma"/>/<see cref="ModalidadeSelecionada"/>.
/// </summary>
/// <remarks>
/// A idade máxima de emissão/formato/tamanho é entregue por task-irmã (PR #900) — não
/// modelada aqui. A base legal 1:N (<see cref="BasesLegais"/>) chegou na PR #898 (issue #549).
/// </remarks>
public sealed class DocumentoExigido : EntityBase
{
    private static readonly string[] ConsequenciasValidas =
    [
        "ELIMINA",
        "RECLASSIFICA_AC",
        "REMOVE_VANTAGEM",
        "PENDENCIA_REENVIO",
    ];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Fase do cronograma do mesmo processo em que o documento é exigido — NOT NULL (Story #554).</summary>
    public Guid ExigidoNaFaseId { get; private set; }

    /// <summary>Id do <c>TipoDocumento</c> vivo no momento da configuração — snapshot-copy (ADR-0061), sem FK cross-módulo.</summary>
    public Guid TipoDocumentoOrigemId { get; private set; }

    /// <summary>Código classificatório congelado — snapshot-copy.</summary>
    public string TipoDocumentoCodigo { get; private set; } = string.Empty;

    /// <summary>Rótulo legível congelado — snapshot-copy.</summary>
    public string TipoDocumentoNome { get; private set; } = string.Empty;

    /// <summary>Categoria classificatória congelada — snapshot-copy.</summary>
    public string TipoDocumentoCategoria { get; private set; } = string.Empty;

    public Aplicabilidade Aplicabilidade { get; private set; }

    public bool Obrigatorio { get; private set; }

    /// <summary>
    /// ∈ {ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM, PENDENCIA_REENVIO} quando presente
    /// (ADR-0074). Campo de transporte — a coerência com <c>ModalidadeSelecionada.AcaoQuandoIndeferido</c>
    /// é validada na PR #903 (CA-05), sem duplicar campo.
    /// </summary>
    public string? ConsequenciaIndeferimento { get; private set; }

    /// <summary>Escopo processo+fase — documentos do mesmo grupo são satisfeitos por uma única apresentação (semântica plena na PR #903).</summary>
    public Guid? GrupoSatisfacaoId { get; private set; }

    /// <summary>
    /// Idade máxima de emissão do ARQUIVO apresentado (Story #554, PR #900) — aviso, não
    /// bloqueio de presença; a avaliação em runtime é fora de escopo desta Story.
    /// </summary>
    public IdadeMaximaEmissao? IdadeMaximaEmissao { get; private set; }

    /// <summary>Formato aceito para a apresentação (Story #554, PR #900) — congelado na exigência, não no <c>TipoDocumento</c>.</summary>
    public FormatoPermitido? FormatoPermitido { get; private set; }

    /// <summary>Tamanho máximo em bytes do arquivo apresentado (Story #554, PR #900) — congelado na exigência.</summary>
    public int? TamanhoMaximoBytes { get; private set; }

    private readonly List<CondicaoGatilho> _condicoes = [];

    /// <summary>Gatilho DNF (Story #554, PR #896) — vazia significa "sem gatilho": GERAL é sempre exigida, CONDICIONAL vazia é exigida de ninguém.</summary>
    public IReadOnlyCollection<CondicaoGatilho> Condicoes => _condicoes.AsReadOnly();

    private readonly List<DocumentoExigidoBaseLegal> _basesLegais = [];

    /// <summary>Base legal 1:N (Story #554, PR #898, ADR-0074) — embasamento rastreável e auditável da exigência; validada no gate de publicação, não na escrita.</summary>
    public IReadOnlyCollection<DocumentoExigidoBaseLegal> BasesLegais => _basesLegais.AsReadOnly();

    private DocumentoExigido() { }

    public static Result<DocumentoExigido> Criar(
        Guid exigidoNaFaseId,
        Guid tipoDocumentoOrigemId,
        string tipoDocumentoCodigo,
        string tipoDocumentoNome,
        string tipoDocumentoCategoria,
        Aplicabilidade aplicabilidade,
        bool obrigatorio,
        string? consequenciaIndeferimento,
        Guid? grupoSatisfacaoId,
        IReadOnlyList<CondicaoGatilho> condicoes,
        IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais,
        IdadeMaximaEmissao? idadeMaximaEmissao,
        FormatoPermitido? formatoPermitido,
        int? tamanhoMaximoBytes)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoNome);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoCategoria);

        if (exigidoNaFaseId == Guid.Empty)
        {
            throw new ArgumentException("A fase em que o documento é exigido é obrigatória.", nameof(exigidoNaFaseId));
        }

        if (tipoDocumentoOrigemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem do tipo de documento é obrigatório.", nameof(tipoDocumentoOrigemId));
        }

        if (aplicabilidade == Aplicabilidade.Nenhuma)
        {
            return Result<DocumentoExigido>.Failure(new DomainError(
                "DocumentoExigido.AplicabilidadeObrigatoria",
                "A aplicabilidade da exigência documental é obrigatória e deve ser GERAL ou CONDICIONAL."));
        }

        // Normaliza espaços-em-branco para null ANTES de validar — sem isso, " " escapa
        // da checagem de domínio (não é "presente" o bastante para reprovar, nem null o
        // bastante para DeterminaResultado() ignorar) e é persistido cru.
        string? consequenciaNormalizada = string.IsNullOrWhiteSpace(consequenciaIndeferimento)
            ? null
            : consequenciaIndeferimento.Trim();

        if (consequenciaNormalizada is not null
            && !ConsequenciasValidas.Contains(consequenciaNormalizada, StringComparer.Ordinal))
        {
            return Result<DocumentoExigido>.Failure(new DomainError(
                "DocumentoExigido.ConsequenciaIndeferimentoInvalida",
                $"Consequência de indeferimento '{consequenciaNormalizada}' inválida — esperado um de: {string.Join(", ", ConsequenciasValidas)}."));
        }

        if (tamanhoMaximoBytes is <= 0)
        {
            return Result<DocumentoExigido>.Failure(new DomainError(
                "DocumentoExigido.TamanhoMaximoBytesInvalido",
                "O tamanho máximo em bytes, quando presente, deve ser maior que zero."));
        }

        DocumentoExigido documento = new()
        {
            ExigidoNaFaseId = exigidoNaFaseId,
            TipoDocumentoOrigemId = tipoDocumentoOrigemId,
            TipoDocumentoCodigo = tipoDocumentoCodigo.Trim(),
            TipoDocumentoNome = tipoDocumentoNome.Trim(),
            TipoDocumentoCategoria = tipoDocumentoCategoria.Trim(),
            Aplicabilidade = aplicabilidade,
            Obrigatorio = obrigatorio,
            ConsequenciaIndeferimento = consequenciaNormalizada,
            GrupoSatisfacaoId = grupoSatisfacaoId,
            IdadeMaximaEmissao = idadeMaximaEmissao,
            FormatoPermitido = formatoPermitido,
            TamanhoMaximoBytes = tamanhoMaximoBytes,
        };

        // CA-01 (Story #554, issue #547/#892): GERAL nunca convive com condição viva —
        // agora conectado à coleção REAL (PR #896), não mais ao parâmetro sintético da PR #895.
        DomainError? coerencia = documento.GarantirCoerenciaAplicabilidade(condicoes.Count > 0);
        if (coerencia is not null)
        {
            return Result<DocumentoExigido>.Failure(coerencia);
        }

        foreach (CondicaoGatilho condicao in condicoes)
        {
            condicao.VincularDocumentoExigido(documento.Id);
            documento._condicoes.Add(condicao);
        }

        foreach (DocumentoExigidoBaseLegal baseLegal in basesLegais)
        {
            baseLegal.VincularDocumentoExigido(documento.Id);
            documento._basesLegais.Add(baseLegal);
        }

        return Result<DocumentoExigido>.Success(documento);
    }

    /// <summary>
    /// Reidrata uma exigência a partir de uma <see cref="VersaoConfiguracao"/> congelada,
    /// <b>preservando o <see cref="EntityBase.Id"/></b> — que <see cref="Criar"/> não
    /// aceita, por decisão (Story #554, PR #903, CA-09). Diferente das demais entidades-filhas
    /// do agregado (ADR-0110 D2 — só <see cref="EtapaProcesso.Id"/> era preservado, porque
    /// só ele era referenciado por outro bloco do envelope), <c>DocumentoExigido.Id</c>
    /// passa a ser o segundo: é o <c>exigenciaId</c> que o resolvedor de exigências
    /// documentais usa para correlacionar uma apresentação à exigência certa — sem
    /// preservá-lo, cada retificação trocaria silenciosamente a identidade de exigências
    /// inalteradas, e apresentações já vinculadas ao <c>exigenciaId</c> antigo deixariam de
    /// resolver.
    /// </summary>
    /// <remarks>
    /// Reidratar não é criar: os dados vêm de um documento com peso jurídico, já validado
    /// quando foi congelado — as guardas aqui são a última linha contra erro de
    /// programação, não revalidação de negócio (essa já rodou em <see cref="Criar"/>,
    /// quando a exigência foi escrita pela primeira vez).
    /// </remarks>
    public static DocumentoExigido Reidratar(
        Guid id,
        Guid exigidoNaFaseId,
        Guid tipoDocumentoOrigemId,
        string tipoDocumentoCodigo,
        string tipoDocumentoNome,
        string tipoDocumentoCategoria,
        Aplicabilidade aplicabilidade,
        bool obrigatorio,
        string? consequenciaIndeferimento,
        Guid? grupoSatisfacaoId,
        IReadOnlyList<CondicaoGatilho> condicoes,
        IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais,
        IdadeMaximaEmissao? idadeMaximaEmissao,
        FormatoPermitido? formatoPermitido,
        int? tamanhoMaximoBytes)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoNome);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumentoCategoria);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A exigência reidratada deve declarar o Id congelado no envelope (CA-09).", nameof(id));
        }

        DocumentoExigido documento = new()
        {
            Id = id,
            ExigidoNaFaseId = exigidoNaFaseId,
            TipoDocumentoOrigemId = tipoDocumentoOrigemId,
            TipoDocumentoCodigo = tipoDocumentoCodigo.Trim(),
            TipoDocumentoNome = tipoDocumentoNome.Trim(),
            TipoDocumentoCategoria = tipoDocumentoCategoria.Trim(),
            Aplicabilidade = aplicabilidade,
            Obrigatorio = obrigatorio,
            ConsequenciaIndeferimento = consequenciaIndeferimento,
            GrupoSatisfacaoId = grupoSatisfacaoId,
            IdadeMaximaEmissao = idadeMaximaEmissao,
            FormatoPermitido = formatoPermitido,
            TamanhoMaximoBytes = tamanhoMaximoBytes,
        };

        foreach (CondicaoGatilho condicao in condicoes)
        {
            condicao.VincularDocumentoExigido(documento.Id);
            documento._condicoes.Add(condicao);
        }

        foreach (DocumentoExigidoBaseLegal baseLegal in basesLegais)
        {
            baseLegal.VincularDocumentoExigido(documento.Id);
            documento._basesLegais.Add(baseLegal);
        }

        return documento;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Determina se a exigência "conta" para efeito de resultado — obrigatória ou com
    /// qualquer consequência de indeferimento declarada. Usado pela trava CA-01 (Story
    /// #554, issue #547: <c>CONDICIONAL</c> vazia que determina resultado bloqueia
    /// publicação) e, futuramente, pelo gate de base legal (PR #898/#549).
    /// </summary>
    public bool DeterminaResultado() => Obrigatorio || ConsequenciaIndeferimento is not null;

    /// <summary>
    /// Projeção "somente <see cref="StatusBaseLegal.Resolvido"/>" (Story #554, PR #898, CA-06)
    /// — o que a PR #903 (#548) materializa no bloco congelado; bases
    /// <see cref="StatusBaseLegal.Pendente"/> são rascunho e nunca aparecem aqui.
    /// </summary>
    public IEnumerable<DocumentoExigidoBaseLegal> BasesLegaisResolvidas() =>
        _basesLegais.Where(static b => b.Status == StatusBaseLegal.Resolvido);

    /// <summary>
    /// A exigência se aplica a um candidato com estes fatos? GERAL sempre se aplica — o
    /// gatilho nunca é avaliado. CONDICIONAL depende do predicado DNF (PR #896): zero
    /// cláusulas vivas nunca casa com ninguém, mesma semântica de
    /// <see cref="PredicadoDnf.Avaliar"/> e de CA-01 ("exigida de ninguém"). Usado tanto
    /// pelo resolvedor de exigências documentais (PR #903, por candidato real) quanto pelo
    /// gate de conformidade legal (PR #903, <see cref="Services.AvaliadorConformidadeLegal"/>
    /// — por um fato sintético fixo, ex. só <c>MODALIDADE</c>, para provar cobertura
    /// incondicional de uma modalidade inteira).
    /// </summary>
    public bool AplicavelPara(IReadOnlyDictionary<string, JsonElement> fatosResolvidos)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);

        if (Aplicabilidade == Aplicabilidade.Geral)
        {
            return true;
        }

        if (_condicoes.Count == 0)
        {
            return false;
        }

        // O dado já foi validado quando a exigência foi escrita (CondicaoGatilho.Criar) —
        // mesmo raciocínio de CondicaoGatilho.ParaCondicaoDnf(), que esta chamada usa por
        // baixo: reidratar/reavaliar não revalida.
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. _condicoes.Select(static c => (c.Clausula, c.ParaCondicaoDnf()))]).Value!;

        return predicado.Avaliar(fatosResolvidos);
    }

    /// <summary>
    /// Coerência entre <see cref="Aplicabilidade"/> e a existência de condição de gatilho
    /// viva (CA-01, Story #554, ADR-0071): <c>GERAL</c> nunca convive com condição viva.
    /// Chamado por <see cref="Criar"/> contra a coleção real de <see cref="Condicoes"/>.
    /// </summary>
    public DomainError? GarantirCoerenciaAplicabilidade(bool possuiCondicaoViva)
    {
        if (Aplicabilidade == Aplicabilidade.Geral && possuiCondicaoViva)
        {
            return new DomainError(
                "DocumentoExigido.GeralComCondicao",
                $"A exigência '{TipoDocumentoCodigo}' é GERAL e não pode conviver com condição de gatilho viva.");
        }

        return null;
    }
}
