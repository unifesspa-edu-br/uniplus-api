namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Unidade federativa (estado/UF), segundo nível da hierarquia geográfica do Geo,
/// situada num <see cref="Pais"/>. Reference data sem soft-delete (ADR-0092) —
/// deriva de <see cref="EntityBase"/> puro.
/// </summary>
/// <remarks>
/// Chave natural <see cref="Uf"/> (UNIQUE) — base do upsert idempotente do ETL.
/// A faixa geral de CEP da UF fica em <see cref="CepInicial"/>/<see cref="CepFinal"/>;
/// as múltiplas faixas (capital/interior) vão na entidade <see cref="EstadoFaixaCep"/>.
/// Coordenada em <c>geography(Point,4326)</c> (ADR-0091); lat/long também
/// preservados em <see cref="Latitude"/>/<see cref="Longitude"/>.
/// </remarks>
public sealed class Estado : EntityBase
{
    /// <summary>FK intra-banco para o <see cref="Pais"/> (ADR-0054).</summary>
    public Guid PaisId { get; private set; }

    /// <summary>Sigla da unidade federativa (chave natural, ex.: <c>PA</c>).</summary>
    public string Uf { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    /// <summary>Nome sem acentos (origem <c>*_sem_acento</c> da DNE) — acelera busca futura.</summary>
    public string? NomeNormalizado { get; private set; }

    public string? Regiao { get; private set; }

    public string? Capital { get; private set; }

    /// <summary>Código IBGE da UF (2 dígitos), quando informado.</summary>
    public string? CodigoIbge { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    /// <summary>Coordenada geográfica (SRID 4326) — <c>geography(Point,4326)</c> (ADR-0091).</summary>
    public Point? Coordenada { get; private set; }

    /// <summary>Início da faixa geral de CEP da UF (origem <c>faixa_ini</c>).</summary>
    public string? CepInicial { get; private set; }

    /// <summary>Fim da faixa geral de CEP da UF (origem <c>faixa_fim</c>).</summary>
    public string? CepFinal { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando a UF some da release vigente (política de stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private Estado()
    {
    }

    /// <summary>
    /// Importa um Estado a partir de valores já tipados (parse tolerante no ETL).
    /// Valida só o mínimo: <paramref name="paisId"/>, chave natural
    /// (<paramref name="uf"/>), <paramref name="nome"/> e proveniência
    /// (<paramref name="versaoDataset"/>).
    /// </summary>
    public static Result<Estado> Importar(
        Guid paisId,
        string uf,
        string nome,
        string? nomeNormalizado,
        string? regiao,
        string? capital,
        string? codigoIbge,
        decimal? latitude,
        decimal? longitude,
        Point? coordenada,
        string? cepInicial,
        string? cepFinal,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(uf);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (paisId == Guid.Empty)
        {
            return Result<Estado>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoPaisObrigatorio,
                "País do Estado é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(uf))
        {
            return Result<Estado>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoUfObrigatoria,
                "UF do Estado é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Estado>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoNomeObrigatorio,
                "Nome do Estado é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<Estado>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.EstadoVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do Estado é obrigatória."));
        }

        var estado = new Estado
        {
            PaisId = paisId,
            Uf = GeoTexto.NormalizarChaveMaiuscula(uf),
            Nome = nome.Trim(),
            NomeNormalizado = GeoTexto.NormalizarOpcional(nomeNormalizado),
            Regiao = GeoTexto.NormalizarOpcional(regiao),
            Capital = GeoTexto.NormalizarOpcional(capital),
            CodigoIbge = GeoTexto.NormalizarOpcional(codigoIbge),
            Latitude = latitude,
            Longitude = longitude,
            Coordenada = coordenada,
            CepInicial = GeoTexto.NormalizarOpcional(cepInicial),
            CepFinal = GeoTexto.NormalizarOpcional(cepFinal),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<Estado>.Success(estado);
    }
}
