namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Faixa de CEP de um <see cref="Bairro"/> (origem <c>bairro_faixa</c> da DNE).
/// Reference data sem soft-delete (ADR-0092). Consistência/sobreposição da faixa
/// ficam no ETL/lookup (F3/F4).
/// </summary>
public sealed class BairroFaixaCep : EntityBase
{
    /// <summary>FK intra-banco para o <see cref="Bairro"/> (ADR-0054).</summary>
    public Guid BairroId { get; private set; }

    public string CepInicial { get; private set; } = string.Empty;

    public string CepFinal { get; private set; } = string.Empty;

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando a faixa some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private BairroFaixaCep()
    {
    }

    /// <summary>Importa uma faixa de CEP de Bairro. Valores já tipados; valida vínculo, limites e proveniência.</summary>
    public static Result<BairroFaixaCep> Importar(
        Guid bairroId,
        string cepInicial,
        string cepFinal,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(cepInicial);
        ArgumentNullException.ThrowIfNull(cepFinal);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (bairroId == Guid.Empty)
        {
            return Result<BairroFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroFaixaCepBairroObrigatorio,
                "Bairro da faixa de CEP é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(cepInicial))
        {
            return Result<BairroFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroFaixaCepInicialObrigatorio,
                "CEP inicial da faixa é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(cepFinal))
        {
            return Result<BairroFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroFaixaCepFinalObrigatorio,
                "CEP final da faixa é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<BairroFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroFaixaCepVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) da faixa de CEP é obrigatória."));
        }

        var faixa = new BairroFaixaCep
        {
            BairroId = bairroId,
            CepInicial = cepInicial.Trim(),
            CepFinal = cepFinal.Trim(),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<BairroFaixaCep>.Success(faixa);
    }

    /// <summary>
    /// Reaplica a proveniência de uma release sobre uma faixa já existente (upsert in
    /// place do ETL), preservando <see cref="EntityBase.Id"/> e a chave natural
    /// <c>(bairro_id, cep_inicial, cep_final)</c>. Valida antes de mutar.
    /// </summary>
    public Result Atualizar(string versaoDataset, bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroFaixaCepVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) da faixa de CEP é obrigatória."));
        }

        VersaoDataset = versaoDataset.Trim();
        Vigente = vigente;

        return Result.Success();
    }
}
