namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Exigência documental de um <see cref="ProcessoSeletivo"/> (Story #554): declara qual
/// documento é exigido, em que fase, com que aplicabilidade e sob qual gatilho DNF
/// (<see cref="Condicoes"/>, PR-b). <c>EntityBase</c> puro (sem soft-delete) — o regime
/// append-only/forense pertence a <see cref="VersaoConfiguracao"/>, não à configuração
/// viva; a coleção do agregado é substituível por inteiro
/// (<see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/>), mesmo padrão de
/// <see cref="FaseCronograma"/>/<see cref="ModalidadeSelecionada"/>.
/// </summary>
/// <remarks>
/// A base legal 1:N (<c>DocumentoExigidoBaseLegal</c>) e a idade máxima de emissão/
/// formato/tamanho são entregues por tasks-irmãs (PR-c/PR-d) — não modeladas aqui.
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
    /// é validada na PR-e (CA-05), sem duplicar campo.
    /// </summary>
    public string? ConsequenciaIndeferimento { get; private set; }

    /// <summary>Escopo processo+fase — documentos do mesmo grupo são satisfeitos por uma única apresentação (semântica plena na PR-e).</summary>
    public Guid? GrupoSatisfacaoId { get; private set; }

    private readonly List<CondicaoGatilho> _condicoes = [];

    /// <summary>Gatilho DNF (Story #554, PR-b) — vazia significa "sem gatilho": GERAL é sempre exigida, CONDICIONAL vazia é exigida de ninguém.</summary>
    public IReadOnlyCollection<CondicaoGatilho> Condicoes => _condicoes.AsReadOnly();

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
        IReadOnlyList<CondicaoGatilho> condicoes)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
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
        };

        // CA-01 (Story #554, issue #547/#892): GERAL nunca convive com condição viva —
        // agora conectado à coleção REAL (PR-b), não mais ao parâmetro sintético da PR-a.
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

        return Result<DocumentoExigido>.Success(documento);
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Determina se a exigência "conta" para efeito de resultado — obrigatória ou com
    /// qualquer consequência de indeferimento declarada. Usado pela trava CA-01 (Story
    /// #554, issue #547: <c>CONDICIONAL</c> vazia que determina resultado bloqueia
    /// publicação) e, futuramente, pelo gate de base legal (PR-c/#549).
    /// </summary>
    public bool DeterminaResultado() => Obrigatorio || ConsequenciaIndeferimento is not null;

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
