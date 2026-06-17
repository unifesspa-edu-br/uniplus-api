namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Satélite socioeconômico IBGE de uma <see cref="Cidade"/> (1:1). Reference data
/// sem soft-delete (ADR-0092). Todos os indicadores são <c>nullable</c>: ~27% dos
/// municípios trazem <c>'-'</c> em <see cref="MortalidadeInfantil"/> e o ETL degrada
/// para <see langword="null"/> (parse tolerante, F3) — só o vínculo e a proveniência
/// são obrigatórios. <see cref="Aniversario"/> é <c>string</c> "DD/MM" (sem ano, não é data).
/// </summary>
public sealed class CidadeIndicador : EntityBase
{
    /// <summary>FK 1:1 para a <see cref="Cidade"/> (UNIQUE, ADR-0054).</summary>
    public Guid CidadeId { get; private set; }

    public string? Gentilico { get; private set; }

    public string? Prefeito { get; private set; }

    public decimal? AreaKm2 { get; private set; }

    public int? PopulacaoResidente { get; private set; }

    public decimal? DensidadeDemografica { get; private set; }

    public decimal? Escolarizacao6a14 { get; private set; }

    public decimal? Idh { get; private set; }

    public decimal? MortalidadeInfantil { get; private set; }

    /// <summary>Receitas (≈bilhões) — <c>decimal(18,2)</c>.</summary>
    public decimal? Receitas { get; private set; }

    /// <summary>Despesas (≈bilhões) — <c>decimal(18,2)</c>.</summary>
    public decimal? Despesas { get; private set; }

    public decimal? PibPerCapita { get; private set; }

    /// <summary>Aniversário do município no formato "DD/MM" (sem ano) — <c>string</c>, não data.</summary>
    public string? Aniversario { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando o vínculo some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private CidadeIndicador()
    {
    }

    /// <summary>
    /// Importa o indicador de uma Cidade. Valores já tipados (parse tolerante no
    /// ETL). Valida só o mínimo: vínculo (<paramref name="cidadeId"/>) e
    /// proveniência (<paramref name="versaoDataset"/>).
    /// </summary>
    public static Result<CidadeIndicador> Importar(
        Guid cidadeId,
        string? gentilico,
        string? prefeito,
        decimal? areaKm2,
        int? populacaoResidente,
        decimal? densidadeDemografica,
        decimal? escolarizacao6a14,
        decimal? idh,
        decimal? mortalidadeInfantil,
        decimal? receitas,
        decimal? despesas,
        decimal? pibPerCapita,
        string? aniversario,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (cidadeId == Guid.Empty)
        {
            return Result<CidadeIndicador>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeIndicadorCidadeObrigatoria,
                "Cidade do indicador é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<CidadeIndicador>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeIndicadorVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do indicador de Cidade é obrigatória."));
        }

        var indicador = new CidadeIndicador
        {
            CidadeId = cidadeId,
            Gentilico = GeoTexto.NormalizarOpcional(gentilico),
            Prefeito = GeoTexto.NormalizarOpcional(prefeito),
            AreaKm2 = areaKm2,
            PopulacaoResidente = populacaoResidente,
            DensidadeDemografica = densidadeDemografica,
            Escolarizacao6a14 = escolarizacao6a14,
            Idh = idh,
            MortalidadeInfantil = mortalidadeInfantil,
            Receitas = receitas,
            Despesas = despesas,
            PibPerCapita = pibPerCapita,
            Aniversario = GeoTexto.NormalizarOpcional(aniversario),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<CidadeIndicador>.Success(indicador);
    }
}
