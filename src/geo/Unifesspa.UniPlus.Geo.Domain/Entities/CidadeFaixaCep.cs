namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Faixa de CEP de uma <see cref="Cidade"/> (origem <c>cidade_faixa</c> da DNE).
/// Reference data sem soft-delete (ADR-0092). A consistência da faixa e a não-
/// sobreposição são responsabilidade do ETL/lookup (F3/F4).
/// </summary>
public sealed class CidadeFaixaCep : EntityBase
{
    /// <summary>FK intra-banco para a <see cref="Cidade"/> (ADR-0054).</summary>
    public Guid CidadeId { get; private set; }

    public string CepInicial { get; private set; } = string.Empty;

    public string CepFinal { get; private set; } = string.Empty;

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando a faixa some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private CidadeFaixaCep()
    {
    }

    /// <summary>
    /// Importa uma faixa de CEP de Cidade. Valores já tipados (parse tolerante no
    /// ETL). Valida vínculo, limites da faixa e proveniência.
    /// </summary>
    public static Result<CidadeFaixaCep> Importar(
        Guid cidadeId,
        string cepInicial,
        string cepFinal,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(cepInicial);
        ArgumentNullException.ThrowIfNull(cepFinal);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (cidadeId == Guid.Empty)
        {
            return Result<CidadeFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeFaixaCepCidadeObrigatoria,
                "Cidade da faixa de CEP é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(cepInicial))
        {
            return Result<CidadeFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeFaixaCepInicialObrigatorio,
                "CEP inicial da faixa é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(cepFinal))
        {
            return Result<CidadeFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeFaixaCepFinalObrigatorio,
                "CEP final da faixa é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<CidadeFaixaCep>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeFaixaCepVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) da faixa de CEP é obrigatória."));
        }

        var faixa = new CidadeFaixaCep
        {
            CidadeId = cidadeId,
            CepInicial = cepInicial.Trim(),
            CepFinal = cepFinal.Trim(),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<CidadeFaixaCep>.Success(faixa);
    }
}
