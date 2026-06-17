namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Satélite socioeconômico IBGE de um <see cref="Estado"/> (1:1). Reference data
/// sem soft-delete (ADR-0092). Todos os indicadores são <c>nullable</c>: a fonte
/// traz <c>'-'</c>/vazio para dado ausente e o ETL degrada para
/// <see langword="null"/> (parse tolerante, F3) — só o vínculo e a proveniência
/// são obrigatórios.
/// </summary>
public sealed class EstadoIndicador : EntityBase
{
    /// <summary>FK 1:1 para o <see cref="Estado"/> (UNIQUE, ADR-0054).</summary>
    public Guid EstadoId { get; private set; }

    public string? Gentilico { get; private set; }

    public string? Governador { get; private set; }

    public decimal? AreaKm2 { get; private set; }

    public int? PopulacaoResidente2022 { get; private set; }

    public decimal? DensidadeDemografica { get; private set; }

    public int? MatriculasEnsinoFundamental2023 { get; private set; }

    public decimal? Idh { get; private set; }

    /// <summary>Receitas brutas (≈bilhões) — <c>decimal(18,2)</c>.</summary>
    public decimal? ReceitasBrutas { get; private set; }

    /// <summary>Despesas brutas (≈bilhões) — <c>decimal(18,2)</c>.</summary>
    public decimal? DespesasBrutas { get; private set; }

    public int? RendimentoMensalPerCapita { get; private set; }

    public int? TotalVeiculos2023 { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando o vínculo some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private EstadoIndicador()
    {
    }

    /// <summary>
    /// Importa o indicador de um Estado. Valores já tipados (parse tolerante no
    /// ETL). Valida só o mínimo: vínculo (<paramref name="estadoId"/>) e
    /// proveniência (<paramref name="versaoDataset"/>).
    /// </summary>
    public static Result<EstadoIndicador> Importar(
        Guid estadoId,
        string? gentilico,
        string? governador,
        decimal? areaKm2,
        int? populacaoResidente2022,
        decimal? densidadeDemografica,
        int? matriculasEnsinoFundamental2023,
        decimal? idh,
        decimal? receitasBrutas,
        decimal? despesasBrutas,
        int? rendimentoMensalPerCapita,
        int? totalVeiculos2023,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (estadoId == Guid.Empty)
        {
            return Result<EstadoIndicador>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoIndicadorEstadoObrigatorio,
                "Estado do indicador é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<EstadoIndicador>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoIndicadorVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do indicador de Estado é obrigatória."));
        }

        var indicador = new EstadoIndicador
        {
            EstadoId = estadoId,
            Gentilico = GeoTexto.NormalizarOpcional(gentilico),
            Governador = GeoTexto.NormalizarOpcional(governador),
            AreaKm2 = areaKm2,
            PopulacaoResidente2022 = populacaoResidente2022,
            DensidadeDemografica = densidadeDemografica,
            MatriculasEnsinoFundamental2023 = matriculasEnsinoFundamental2023,
            Idh = idh,
            ReceitasBrutas = receitasBrutas,
            DespesasBrutas = despesasBrutas,
            RendimentoMensalPerCapita = rendimentoMensalPerCapita,
            TotalVeiculos2023 = totalVeiculos2023,
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<EstadoIndicador>.Success(indicador);
    }

    /// <summary>
    /// Reaplica os indicadores de uma release sobre o satélite já existente (upsert
    /// in place do ETL), preservando <see cref="EntityBase.Id"/> e o vínculo
    /// <see cref="EstadoId"/>. Valida o mínimo <strong>antes</strong> de mutar.
    /// </summary>
    public Result Atualizar(
        string? gentilico,
        string? governador,
        decimal? areaKm2,
        int? populacaoResidente2022,
        decimal? densidadeDemografica,
        int? matriculasEnsinoFundamental2023,
        decimal? idh,
        decimal? receitasBrutas,
        decimal? despesasBrutas,
        int? rendimentoMensalPerCapita,
        int? totalVeiculos2023,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoIndicadorVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do indicador de Estado é obrigatória."));
        }

        Gentilico = GeoTexto.NormalizarOpcional(gentilico);
        Governador = GeoTexto.NormalizarOpcional(governador);
        AreaKm2 = areaKm2;
        PopulacaoResidente2022 = populacaoResidente2022;
        DensidadeDemografica = densidadeDemografica;
        MatriculasEnsinoFundamental2023 = matriculasEnsinoFundamental2023;
        Idh = idh;
        ReceitasBrutas = receitasBrutas;
        DespesasBrutas = despesasBrutas;
        RendimentoMensalPerCapita = rendimentoMensalPerCapita;
        TotalVeiculos2023 = totalVeiculos2023;
        VersaoDataset = versaoDataset.Trim();
        Vigente = vigente;

        return Result.Success();
    }
}
