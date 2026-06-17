namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Bairro — nível intermediário situado numa <see cref="Cidade"/>. Reference data
/// sem soft-delete (ADR-0092). Sem código IBGE na fonte: a chave natural é
/// <c>(cidade_id, nome_normalizado)</c>; o id sequencial da DNE fica em
/// <see cref="IdOrigemDne"/> (referência instável entre releases).
/// </summary>
/// <remarks>
/// <see cref="NomeNormalizado"/> é <strong>obrigatório</strong> porque compõe a
/// chave natural (UNIQUE com nulo em PostgreSQL admitiria duplicatas).
/// </remarks>
public sealed class Bairro : EntityBase
{
    /// <summary>FK intra-banco para a <see cref="Cidade"/> (ADR-0054).</summary>
    public Guid CidadeId { get; private set; }

    /// <summary>UF denormalizada da fonte (o vínculo forte é <see cref="CidadeId"/>).</summary>
    public string Uf { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    /// <summary>Nome sem acentos (origem <c>*_sem_acento</c>) — compõe a chave natural; não nulo.</summary>
    public string NomeNormalizado { get; private set; } = string.Empty;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    /// <summary>Coordenada geográfica (SRID 4326) — <c>geography(Point,4326)</c> (ADR-0091).</summary>
    public Point? Coordenada { get; private set; }

    /// <summary>Id sequencial de origem na DNE — referência instável, não é identidade nem único.</summary>
    public string? IdOrigemDne { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando a chave natural some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private Bairro()
    {
    }

    /// <summary>
    /// Importa um Bairro a partir de valores já tipados (parse tolerante no ETL).
    /// Valida o mínimo: <paramref name="cidadeId"/>, <paramref name="nome"/>,
    /// <paramref name="nomeNormalizado"/> (compõe a chave natural) e proveniência.
    /// </summary>
    public static Result<Bairro> Importar(
        Guid cidadeId,
        string uf,
        string nome,
        string nomeNormalizado,
        decimal? latitude,
        decimal? longitude,
        Point? coordenada,
        string? idOrigemDne,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(uf);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(nomeNormalizado);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (cidadeId == Guid.Empty)
        {
            return Result<Bairro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroCidadeObrigatoria,
                "Cidade do Bairro é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Bairro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroNomeObrigatorio,
                "Nome do Bairro é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            return Result<Bairro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroNomeNormalizadoObrigatorio,
                "Nome normalizado do Bairro é obrigatório (compõe a chave natural)."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<Bairro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.BairroVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do Bairro é obrigatória."));
        }

        var bairro = new Bairro
        {
            CidadeId = cidadeId,
            Uf = GeoTexto.NormalizarChaveMaiuscula(uf),
            Nome = nome.Trim(),
            NomeNormalizado = GeoTexto.NormalizarTexto(nomeNormalizado),
            Latitude = latitude,
            Longitude = longitude,
            Coordenada = coordenada,
            IdOrigemDne = GeoTexto.NormalizarOpcional(idOrigemDne),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<Bairro>.Success(bairro);
    }
}
