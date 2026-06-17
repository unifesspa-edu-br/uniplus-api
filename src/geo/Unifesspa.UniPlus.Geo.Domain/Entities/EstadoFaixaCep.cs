namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Faixa de CEP de um <see cref="Estado"/> (origem <c>estado_faixa</c> da DNE) —
/// uma UF pode ter várias (capital e interior). Reference data sem soft-delete
/// (ADR-0092). A <see cref="Descricao"/> preserva a coluna <c>regiao</c> da fonte
/// (ex.: "Capital: Rio Branco").
/// </summary>
public sealed class EstadoFaixaCep : EntityBase
{
    /// <summary>FK intra-banco para o <see cref="Estado"/> (ADR-0054).</summary>
    public Guid EstadoId { get; private set; }

    public string CepInicial { get; private set; } = string.Empty;

    public string CepFinal { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando a faixa some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private EstadoFaixaCep()
    {
    }

    /// <summary>
    /// Importa uma faixa de CEP de Estado. Valores já tipados (parse tolerante no
    /// ETL). Valida vínculo, limites da faixa e proveniência. A consistência
    /// <c>cep_inicial ≤ cep_final</c> e a não-sobreposição são responsabilidade do
    /// ETL/lookup (F3/F4) — a carga em lote é tolerante por construção (ADR-0092).
    /// </summary>
    public static Result<EstadoFaixaCep> Importar(
        Guid estadoId,
        string cepInicial,
        string cepFinal,
        string? descricao,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(cepInicial);
        ArgumentNullException.ThrowIfNull(cepFinal);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (estadoId == Guid.Empty)
        {
            return Result<EstadoFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoFaixaCepEstadoObrigatorio,
                "Estado da faixa de CEP é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(cepInicial))
        {
            return Result<EstadoFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoFaixaCepInicialObrigatorio,
                "CEP inicial da faixa é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(cepFinal))
        {
            return Result<EstadoFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoFaixaCepFinalObrigatorio,
                "CEP final da faixa é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<EstadoFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoFaixaCepVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) da faixa de CEP é obrigatória."));
        }

        var faixa = new EstadoFaixaCep
        {
            EstadoId = estadoId,
            CepInicial = cepInicial.Trim(),
            CepFinal = cepFinal.Trim(),
            Descricao = GeoTexto.NormalizarOpcional(descricao),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<EstadoFaixaCep>.Success(faixa);
    }
}
