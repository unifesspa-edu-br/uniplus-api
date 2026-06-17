namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Faixa de CEP de um <see cref="Distrito"/> (origem <c>distrito_faixa</c> da DNE).
/// Reference data sem soft-delete (ADR-0092). Consistência/sobreposição da faixa
/// ficam no ETL/lookup (F3/F4).
/// </summary>
public sealed class DistritoFaixaCep : EntityBase
{
    /// <summary>FK intra-banco para o <see cref="Distrito"/> (ADR-0054).</summary>
    public Guid DistritoId { get; private set; }

    public string CepInicial { get; private set; } = string.Empty;

    public string CepFinal { get; private set; } = string.Empty;

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando a faixa some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private DistritoFaixaCep()
    {
    }

    /// <summary>Importa uma faixa de CEP de Distrito. Valores já tipados; valida vínculo, limites e proveniência.</summary>
    public static Result<DistritoFaixaCep> Importar(
        Guid distritoId,
        string cepInicial,
        string cepFinal,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(cepInicial);
        ArgumentNullException.ThrowIfNull(cepFinal);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (distritoId == Guid.Empty)
        {
            return Result<DistritoFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoFaixaCepDistritoObrigatorio,
                "Distrito da faixa de CEP é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(cepInicial))
        {
            return Result<DistritoFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoFaixaCepInicialObrigatorio,
                "CEP inicial da faixa é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(cepFinal))
        {
            return Result<DistritoFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoFaixaCepFinalObrigatorio,
                "CEP final da faixa é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<DistritoFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoFaixaCepVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) da faixa de CEP é obrigatória."));
        }

        var faixa = new DistritoFaixaCep
        {
            DistritoId = distritoId,
            CepInicial = cepInicial.Trim(),
            CepFinal = cepFinal.Trim(),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<DistritoFaixaCep>.Success(faixa);
    }
}
