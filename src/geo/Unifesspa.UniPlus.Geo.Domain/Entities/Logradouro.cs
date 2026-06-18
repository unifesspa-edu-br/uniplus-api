namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Logradouro — folha da hierarquia (a entrada por CEP, maior volume do módulo,
/// ~1,4M linhas). Reference data sem soft-delete (ADR-0092). O <see cref="Cep"/> é
/// <strong>indexado mas não único</strong> (o CEP geral de cidade cobre vários
/// logradouros); a idempotência do upsert vem da chave composta
/// <c>(cep, nome_normalizado, cidade_id)</c>.
/// </summary>
/// <remarks>
/// <see cref="NomeNormalizado"/> é <c>NOT NULL</c> por compor a chave de upsert
/// (UNIQUE com nulo admitiria duplicatas). <see cref="DistritoId"/>/<see cref="BairroId"/>
/// são opcionais (a fonte traz distrito nulo com frequência). A coerência de que
/// distrito/bairro pertencem à mesma cidade é responsabilidade do ETL (F3, dado
/// autoritativo externo, ADR-0092) — não há FK composta cruzando cidade.
/// </remarks>
public sealed class Logradouro : EntityBase
{
    /// <summary>CEP de 8 dígitos — indexado, NÃO único (CEP geral cobre vários logradouros).</summary>
    public string Cep { get; private set; } = string.Empty;

    /// <summary>Tipo do logradouro (ex.: "Rua", "Praça"), quando informado.</summary>
    public string? Tipo { get; private set; }

    /// <summary>Nome do logradouro, sem o tipo (origem <c>nome_logradouro</c>, ex.: "A").</summary>
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Texto completo do logradouro com o tipo (origem <c>logradouro</c>, ex.: "Rua A"), quando informado.</summary>
    public string? NomeCompleto { get; private set; }

    /// <summary>
    /// <strong>Texto completo</strong> sem acentos, em caixa-baixa (origem
    /// <c>logradouro_sem_acento</c>, ex.: "rua a") — é a coluna de busca (autocomplete por
    /// tipo + nome, índice trigram) e compõe a chave de upsert; não nulo. Diverge de
    /// Distrito/Bairro (que guardam o nome sem o tipo) porque o logradouro tem o tipo numa
    /// coluna à parte: incluí-lo aqui torna a busca útil e endurece a chave contra a
    /// colisão de CEP-geral (#707).
    /// </summary>
    public string NomeNormalizado { get; private set; } = string.Empty;

    /// <summary>FK intra-banco para a <see cref="Cidade"/> (obrigatória, ADR-0054).</summary>
    public Guid CidadeId { get; private set; }

    /// <summary>FK intra-banco opcional para o <see cref="Distrito"/> (a fonte traz nulo com frequência).</summary>
    public Guid? DistritoId { get; private set; }

    /// <summary>FK intra-banco opcional para o <see cref="Bairro"/>.</summary>
    public Guid? BairroId { get; private set; }

    /// <summary>UF denormalizada da fonte (o vínculo forte é <see cref="CidadeId"/>).</summary>
    public string Uf { get; private set; } = string.Empty;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    /// <summary>Coordenada geográfica (SRID 4326) — <c>geography(Point,4326)</c> (ADR-0091).</summary>
    public Point? Coordenada { get; private set; }

    /// <summary>Atributo do dado nos Correios (origem <c>cep_ativo</c> 'S'/'N'). Distinto de <see cref="Vigente"/>.</summary>
    public bool Ativo { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary>
    /// Proveniência da recarga: <see langword="false"/> quando a chave natural some
    /// entre releases. <strong>Distinto</strong> de <see cref="Ativo"/> (atributo do
    /// dado nos Correios).
    /// </summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private Logradouro()
    {
    }

    /// <summary>
    /// Importa um Logradouro a partir de valores já tipados (parse tolerante e a
    /// conversão <c>'S'/'N' → bool</c> moram no ETL). Valida o mínimo: chave de
    /// upsert (<paramref name="cep"/>, <paramref name="nomeNormalizado"/>,
    /// <paramref name="cidadeId"/>), <paramref name="nome"/> e proveniência.
    /// </summary>
    public static Result<Logradouro> Importar(
        string cep,
        string? tipo,
        string nome,
        string? nomeCompleto,
        string nomeNormalizado,
        Guid cidadeId,
        Guid? distritoId,
        Guid? bairroId,
        string uf,
        decimal? latitude,
        decimal? longitude,
        Point? coordenada,
        bool ativo,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(cep);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(nomeNormalizado);
        ArgumentNullException.ThrowIfNull(uf);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (string.IsNullOrWhiteSpace(cep))
        {
            return Result<Logradouro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroCepObrigatorio,
                "CEP do Logradouro é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Logradouro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroNomeObrigatorio,
                "Nome do Logradouro é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            return Result<Logradouro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroNomeNormalizadoObrigatorio,
                "Nome normalizado do Logradouro é obrigatório (compõe a chave de upsert)."));
        }

        if (cidadeId == Guid.Empty)
        {
            return Result<Logradouro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroCidadeObrigatoria,
                "Cidade do Logradouro é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<Logradouro>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do Logradouro é obrigatória."));
        }

        var logradouro = new Logradouro
        {
            Cep = cep.Trim(),
            Tipo = GeoTexto.NormalizarOpcional(tipo),
            Nome = nome.Trim(),
            NomeCompleto = GeoTexto.NormalizarOpcional(nomeCompleto),
            NomeNormalizado = GeoTexto.NormalizarTexto(nomeNormalizado),
            CidadeId = cidadeId,
            DistritoId = distritoId,
            BairroId = bairroId,
            Uf = GeoTexto.NormalizarChaveMaiuscula(uf),
            Latitude = latitude,
            Longitude = longitude,
            Coordenada = coordenada,
            Ativo = ativo,
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<Logradouro>.Success(logradouro);
    }
}
