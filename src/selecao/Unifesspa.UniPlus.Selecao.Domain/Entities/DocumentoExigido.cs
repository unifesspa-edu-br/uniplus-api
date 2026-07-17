namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Exigência documental de um <see cref="ProcessoSeletivo"/> (Story #554, PR-a): declara
/// qual documento é exigido, em que fase e com que aplicabilidade. <c>EntityBase</c>
/// puro (sem soft-delete) — o regime append-only/forense pertence a
/// <see cref="VersaoConfiguracao"/>, não à configuração viva; a coleção do agregado é
/// substituível por inteiro (<see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/>),
/// mesmo padrão de <see cref="FaseCronograma"/>/<see cref="ModalidadeSelecionada"/>.
/// </summary>
/// <remarks>
/// O gatilho DNF (<c>CondicaoGatilho</c>), a base legal 1:N
/// (<c>DocumentoExigidoBaseLegal</c>) e a idade máxima de emissão/formato/tamanho são
/// entregues por tasks-irmãs (PR-b/PR-c/PR-d) — não modelados aqui.
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
        Guid? grupoSatisfacaoId)
    {
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

        if (!string.IsNullOrWhiteSpace(consequenciaIndeferimento)
            && !ConsequenciasValidas.Contains(consequenciaIndeferimento, StringComparer.Ordinal))
        {
            return Result<DocumentoExigido>.Failure(new DomainError(
                "DocumentoExigido.ConsequenciaIndeferimentoInvalida",
                $"Consequência de indeferimento '{consequenciaIndeferimento}' inválida — esperado um de: {string.Join(", ", ConsequenciasValidas)}."));
        }

        return Result<DocumentoExigido>.Success(new DocumentoExigido
        {
            ExigidoNaFaseId = exigidoNaFaseId,
            TipoDocumentoOrigemId = tipoDocumentoOrigemId,
            TipoDocumentoCodigo = tipoDocumentoCodigo.Trim(),
            TipoDocumentoNome = tipoDocumentoNome.Trim(),
            TipoDocumentoCategoria = tipoDocumentoCategoria.Trim(),
            Aplicabilidade = aplicabilidade,
            Obrigatorio = obrigatorio,
            ConsequenciaIndeferimento = consequenciaIndeferimento,
            GrupoSatisfacaoId = grupoSatisfacaoId,
        });
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    /// <summary>
    /// Determina se a exigência "conta" para efeito de resultado — obrigatória ou com
    /// qualquer consequência de indeferimento declarada. Usado pela trava CA-01
    /// (<c>CONDICIONAL</c> vazia que determina resultado bloqueia publicação) e,
    /// futuramente, pelo gate de base legal (PR-c/#549).
    /// </summary>
    public bool DeterminaResultado() => Obrigatorio || ConsequenciaIndeferimento is not null;

    /// <summary>
    /// Coerência entre <see cref="Aplicabilidade"/> e a existência de condição de gatilho
    /// viva (CA-01, ADR-0071): <c>GERAL</c> nunca convive com condição viva. O parâmetro é
    /// sintético nesta task — <c>CondicaoGatilho</c> (PR-b) ainda não existe; a PR-b conecta
    /// este guard à coleção real ao permitir cadastrar condições.
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
