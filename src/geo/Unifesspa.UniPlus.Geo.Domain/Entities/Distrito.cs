namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Distrito — nível intermediário situado numa <see cref="Cidade"/>. Reference data
/// sem soft-delete (ADR-0092). Não tem código IBGE na fonte: a chave natural é
/// <c>(cidade_id, nome_normalizado)</c>; o id sequencial da DNE é guardado apenas
/// como <see cref="IdOrigemDne"/> (referência de origem, instável entre releases).
/// </summary>
/// <remarks>
/// <see cref="NomeNormalizado"/> é <strong>obrigatório</strong> porque compõe a
/// chave natural: em PostgreSQL um UNIQUE com coluna nula admite múltiplos nulos,
/// o que furaria a idempotência do upsert — por isso o valor não pode ser nulo.
/// </remarks>
public sealed class Distrito : EntityBase
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
    private Distrito()
    {
    }

    /// <summary>
    /// Importa um Distrito a partir de valores já tipados (parse tolerante no ETL).
    /// Valida o mínimo: <paramref name="cidadeId"/>, <paramref name="nome"/>,
    /// <paramref name="nomeNormalizado"/> (compõe a chave natural) e proveniência.
    /// </summary>
    public static Result<Distrito> Importar(
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
            return Result<Distrito>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoCidadeObrigatoria,
                "Cidade do Distrito é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Distrito>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoNomeObrigatorio,
                "Nome do Distrito é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            return Result<Distrito>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoNomeNormalizadoObrigatorio,
                "Nome normalizado do Distrito é obrigatório (compõe a chave natural)."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<Distrito>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do Distrito é obrigatória."));
        }

        var distrito = new Distrito
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

        return Result<Distrito>.Success(distrito);
    }

    /// <summary>
    /// Reaplica os dados de uma release sobre um Distrito já existente (upsert in place
    /// do ETL), preservando <see cref="EntityBase.Id"/> (referenciado por faixas e por
    /// <see cref="Logradouro"/>) e a chave natural <c>(cidade_id, nome_normalizado)</c>.
    /// Valida o mínimo <strong>antes</strong> de mutar — em falha, o estado não é tocado.
    /// </summary>
    public Result Atualizar(
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
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoCidadeObrigatoria,
                "Cidade do Distrito é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoNomeObrigatorio,
                "Nome do Distrito é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoNomeNormalizadoObrigatorio,
                "Nome normalizado do Distrito é obrigatório (compõe a chave natural)."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.DistritoVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do Distrito é obrigatória."));
        }

        CidadeId = cidadeId;
        Uf = GeoTexto.NormalizarChaveMaiuscula(uf);
        Nome = nome.Trim();
        NomeNormalizado = GeoTexto.NormalizarTexto(nomeNormalizado);
        Latitude = latitude;
        Longitude = longitude;
        Coordenada = coordenada;
        IdOrigemDne = GeoTexto.NormalizarOpcional(idOrigemDne);
        VersaoDataset = versaoDataset.Trim();
        Vigente = vigente;

        return Result.Success();
    }
}
