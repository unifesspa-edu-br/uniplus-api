namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Município — eixo central do Geo, referenciado por outros módulos por
/// <see cref="CodigoIbge"/> (#587: Campus/LocalOferta). Reference data sem
/// soft-delete (ADR-0092). Os campos <strong>territoriais</strong> (meso/micro/
/// região intermediária e imediata, código + nome) ficam embutidos aqui (identidade
/// geográfica); os socioeconômicos vão no satélite <see cref="CidadeIndicador"/>.
/// </summary>
/// <remarks>
/// Chave natural <see cref="CodigoIbge"/> (7 dígitos, UNIQUE) — homônimos em UFs
/// distintas coexistem (a chave é o código, não o nome). <see cref="NomeNormalizado"/>
/// preserva o <c>cidade_sem_acento</c> da fonte e ganha índice trigram para o
/// autocomplete acento-insensível da F4 — não compõe a chave (fica nullable).
/// </remarks>
public sealed class Cidade : EntityBase
{
    /// <summary>FK intra-banco para o <see cref="Estado"/> (ADR-0054).</summary>
    public Guid EstadoId { get; private set; }

    /// <summary>UF do município (denormalizada da fonte; o vínculo forte é <see cref="EstadoId"/>).</summary>
    public string Uf { get; private set; } = string.Empty;

    /// <summary>Código IBGE de 7 dígitos (chave natural, UNIQUE).</summary>
    public string CodigoIbge { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    /// <summary>Nome sem acentos (origem <c>cidade_sem_acento</c>) — base do autocomplete (F4).</summary>
    public string? NomeNormalizado { get; private set; }

    public string? Ddd { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    /// <summary>Coordenada geográfica (SRID 4326) — <c>geography(Point,4326)</c> (ADR-0091).</summary>
    public Point? Coordenada { get; private set; }

    public string? MesorregiaoCodigo { get; private set; }

    public string? MesorregiaoNome { get; private set; }

    public string? MicrorregiaoCodigo { get; private set; }

    public string? MicrorregiaoNome { get; private set; }

    public string? RegiaoIntermediariaCodigo { get; private set; }

    public string? RegiaoIntermediariaNome { get; private set; }

    public string? RegiaoImediataCodigo { get; private set; }

    public string? RegiaoImediataNome { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando o código IBGE some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private Cidade()
    {
    }

    /// <summary>
    /// Importa um Município a partir de valores já tipados (parse tolerante no ETL).
    /// Valida só o mínimo: <paramref name="estadoId"/>, chave natural
    /// (<paramref name="codigoIbge"/>), <paramref name="nome"/> e proveniência
    /// (<paramref name="versaoDataset"/>).
    /// </summary>
    public static Result<Cidade> Importar(
        Guid estadoId,
        string uf,
        string codigoIbge,
        string nome,
        string? nomeNormalizado,
        string? ddd,
        decimal? latitude,
        decimal? longitude,
        Point? coordenada,
        string? mesorregiaoCodigo,
        string? mesorregiaoNome,
        string? microrregiaoCodigo,
        string? microrregiaoNome,
        string? regiaoIntermediariaCodigo,
        string? regiaoIntermediariaNome,
        string? regiaoImediataCodigo,
        string? regiaoImediataNome,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(uf);
        ArgumentNullException.ThrowIfNull(codigoIbge);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (estadoId == Guid.Empty)
        {
            return Result<Cidade>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeEstadoObrigatorio,
                "Estado da Cidade é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(codigoIbge))
        {
            return Result<Cidade>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeCodigoIbgeObrigatorio,
                "Código IBGE da Cidade é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Cidade>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeNomeObrigatorio,
                "Nome da Cidade é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<Cidade>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) da Cidade é obrigatória."));
        }

        var cidade = new Cidade
        {
            EstadoId = estadoId,
            Uf = GeoTexto.NormalizarChaveMaiuscula(uf),
            CodigoIbge = codigoIbge.Trim(),
            Nome = nome.Trim(),
            NomeNormalizado = GeoTexto.NormalizarBuscaOpcional(nomeNormalizado),
            Ddd = GeoTexto.NormalizarOpcional(ddd),
            Latitude = latitude,
            Longitude = longitude,
            Coordenada = coordenada,
            MesorregiaoCodigo = GeoTexto.NormalizarOpcional(mesorregiaoCodigo),
            MesorregiaoNome = GeoTexto.NormalizarOpcional(mesorregiaoNome),
            MicrorregiaoCodigo = GeoTexto.NormalizarOpcional(microrregiaoCodigo),
            MicrorregiaoNome = GeoTexto.NormalizarOpcional(microrregiaoNome),
            RegiaoIntermediariaCodigo = GeoTexto.NormalizarOpcional(regiaoIntermediariaCodigo),
            RegiaoIntermediariaNome = GeoTexto.NormalizarOpcional(regiaoIntermediariaNome),
            RegiaoImediataCodigo = GeoTexto.NormalizarOpcional(regiaoImediataCodigo),
            RegiaoImediataNome = GeoTexto.NormalizarOpcional(regiaoImediataNome),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<Cidade>.Success(cidade);
    }

    /// <summary>
    /// Reaplica os dados de uma release sobre um Município já existente (upsert in
    /// place do ETL), preservando <see cref="EntityBase.Id"/> (referenciado por
    /// Indicador/faixas e por outros módulos via código IBGE) e a chave natural
    /// <see cref="CodigoIbge"/>. Os campos territoriais são reaplicados juntos
    /// (a fonte os agrega por código IBGE antes da chamada). Valida o mínimo
    /// <strong>antes</strong> de mutar — em falha, o estado não é tocado.
    /// </summary>
    public Result Atualizar(
        Guid estadoId,
        string uf,
        string nome,
        string? nomeNormalizado,
        string? ddd,
        decimal? latitude,
        decimal? longitude,
        Point? coordenada,
        string? mesorregiaoCodigo,
        string? mesorregiaoNome,
        string? microrregiaoCodigo,
        string? microrregiaoNome,
        string? regiaoIntermediariaCodigo,
        string? regiaoIntermediariaNome,
        string? regiaoImediataCodigo,
        string? regiaoImediataNome,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(uf);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (estadoId == Guid.Empty)
        {
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeEstadoObrigatorio,
                "Estado da Cidade é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeNomeObrigatorio,
                "Nome da Cidade é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result.Failure(new DomainError(
                GeoReferenceDataErrorCodes.CidadeVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) da Cidade é obrigatória."));
        }

        EstadoId = estadoId;
        Uf = GeoTexto.NormalizarChaveMaiuscula(uf);
        Nome = nome.Trim();
        NomeNormalizado = GeoTexto.NormalizarBuscaOpcional(nomeNormalizado);
        Ddd = GeoTexto.NormalizarOpcional(ddd);
        Latitude = latitude;
        Longitude = longitude;
        Coordenada = coordenada;
        MesorregiaoCodigo = GeoTexto.NormalizarOpcional(mesorregiaoCodigo);
        MesorregiaoNome = GeoTexto.NormalizarOpcional(mesorregiaoNome);
        MicrorregiaoCodigo = GeoTexto.NormalizarOpcional(microrregiaoCodigo);
        MicrorregiaoNome = GeoTexto.NormalizarOpcional(microrregiaoNome);
        RegiaoIntermediariaCodigo = GeoTexto.NormalizarOpcional(regiaoIntermediariaCodigo);
        RegiaoIntermediariaNome = GeoTexto.NormalizarOpcional(regiaoIntermediariaNome);
        RegiaoImediataCodigo = GeoTexto.NormalizarOpcional(regiaoImediataCodigo);
        RegiaoImediataNome = GeoTexto.NormalizarOpcional(regiaoImediataNome);
        VersaoDataset = versaoDataset.Trim();
        Vigente = vigente;

        return Result.Success();
    }
}
