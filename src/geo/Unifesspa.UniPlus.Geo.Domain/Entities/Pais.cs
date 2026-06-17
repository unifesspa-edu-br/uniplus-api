namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Topo da hierarquia geográfica do Geo. Reference data de origem externa
/// (IBGE/DNE) — carregado por ETL, sem CRUD de usuário e sem soft-delete
/// (ADR-0092): deriva de <see cref="EntityBase"/> puro. Em produção é populado
/// <strong>só com o Brasil</strong> (a estrutura já comporta países estrangeiros).
/// </summary>
/// <remarks>
/// Chave natural <see cref="SiglaIso"/> (ISO 3166-1 alpha-3, ex.: <c>BRA</c>) —
/// base da idempotência do upsert do ETL. Toda entidade reference data carrega a
/// proveniência de release (<see cref="VersaoDataset"/> + <see cref="Vigente"/>),
/// distinta de qualquer atributo de atividade do próprio dado.
/// </remarks>
public sealed class Pais : EntityBase
{
    /// <summary>Sigla ISO 3166-1 alpha-3 (chave natural, ex.: <c>BRA</c>).</summary>
    public string SiglaIso { get; private set; } = string.Empty;

    /// <summary>Sigla curta de uso corrente (ex.: <c>BR</c>).</summary>
    public string Sigla { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    /// <summary>Código do país no Banco Central (BCB), quando informado.</summary>
    public string? CodigoBcb { get; private set; }

    /// <summary>Código do país na Receita Federal (RFB), quando informado.</summary>
    public string? CodigoRfb { get; private set; }

    /// <summary>Código do país no SPED, quando informado.</summary>
    public string? CodigoSped { get; private set; }

    /// <summary>Código do país no Siscomex, quando informado.</summary>
    public string? CodigoSiscomex { get; private set; }

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary>
    /// <see langword="false"/> quando a chave natural deixou de aparecer na release
    /// vigente, sem remover a linha (política de stale do ETL). Distinto de qualquer
    /// indicador de atividade do dado.
    /// </summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private Pais()
    {
    }

    /// <summary>
    /// Importa um País a partir de valores já tipados (o parse tolerante mora no
    /// ETL/F3). Valida só o mínimo: chave natural (<paramref name="siglaIso"/>),
    /// <paramref name="nome"/> e proveniência (<paramref name="versaoDataset"/>)
    /// não vazios — dado externo autoritativo não tem regra de negócio mutável.
    /// </summary>
    public static Result<Pais> Importar(
        string siglaIso,
        string sigla,
        string nome,
        string? codigoBcb,
        string? codigoRfb,
        string? codigoSped,
        string? codigoSiscomex,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(siglaIso);
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (string.IsNullOrWhiteSpace(siglaIso))
        {
            return Result<Pais>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.PaisSiglaIsoObrigatoria,
                "Sigla ISO do País é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Pais>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.PaisNomeObrigatorio,
                "Nome do País é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<Pais>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.PaisVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do País é obrigatória."));
        }

        var pais = new Pais
        {
            SiglaIso = GeoTexto.NormalizarChaveMaiuscula(siglaIso),
            Sigla = GeoTexto.NormalizarChaveMaiuscula(sigla),
            Nome = nome.Trim(),
            CodigoBcb = GeoTexto.NormalizarOpcional(codigoBcb),
            CodigoRfb = GeoTexto.NormalizarOpcional(codigoRfb),
            CodigoSped = GeoTexto.NormalizarOpcional(codigoSped),
            CodigoSiscomex = GeoTexto.NormalizarOpcional(codigoSiscomex),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<Pais>.Success(pais);
    }
}
