namespace Unifesspa.UniPlus.Geo.Domain.Entities;

using Unifesspa.UniPlus.Geo.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Complemento de endereçamento por CEP (origem <c>log_complemento</c> da DNE) —
/// ex.: lado par/ímpar, faixa de numeração. Reference data sem soft-delete
/// (ADR-0092). É um atributo do <strong>CEP</strong>, não de um logradouro
/// específico: o mesmo CEP comporta vários complementos e nenhum se vincula a um
/// único logradouro — por isso <strong>não há FK para <see cref="Logradouro"/></strong>.
/// </summary>
/// <remarks>
/// O <see cref="Cep"/> é indexado e <strong>não único</strong> (compartilhado). A
/// idempotência do upsert vem da chave <c>(cep, complemento_normalizado)</c>, que é
/// UNIQUE; <see cref="ComplementoNormalizado"/> é <c>NOT NULL</c> por compô-la.
/// </remarks>
public sealed class LogradouroComplemento : EntityBase
{
    /// <summary>CEP de 8 dígitos — indexado, NÃO único (compartilhado).</summary>
    public string Cep { get; private set; } = string.Empty;

    public string Complemento { get; private set; } = string.Empty;

    /// <summary>Complemento sem acentos — compõe a chave de upsert; não nulo.</summary>
    public string ComplementoNormalizado { get; private set; } = string.Empty;

    /// <summary>Release DNE de origem (AAAAMM) — proveniência da carga (ADR-0092).</summary>
    public string VersaoDataset { get; private set; } = string.Empty;

    /// <summary><see langword="false"/> quando a chave natural some da release vigente (stale do ETL).</summary>
    public bool Vigente { get; private set; } = true;

    // Construtor privado para materialização do EF Core.
    private LogradouroComplemento()
    {
    }

    /// <summary>
    /// Importa um complemento por CEP. Valores já tipados (parse no ETL). Valida o
    /// mínimo: chave de upsert (<paramref name="cep"/>,
    /// <paramref name="complementoNormalizado"/>), texto e proveniência.
    /// </summary>
    public static Result<LogradouroComplemento> Importar(
        string cep,
        string complemento,
        string complementoNormalizado,
        string versaoDataset,
        bool vigente = true)
    {
        ArgumentNullException.ThrowIfNull(cep);
        ArgumentNullException.ThrowIfNull(complemento);
        ArgumentNullException.ThrowIfNull(complementoNormalizado);
        ArgumentNullException.ThrowIfNull(versaoDataset);

        if (string.IsNullOrWhiteSpace(cep))
        {
            return Result<LogradouroComplemento>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroComplementoCepObrigatorio,
                "CEP do complemento é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(complemento))
        {
            return Result<LogradouroComplemento>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroComplementoObrigatorio,
                "Texto do complemento é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(complementoNormalizado))
        {
            return Result<LogradouroComplemento>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroComplementoNormalizadoObrigatorio,
                "Complemento normalizado é obrigatório (compõe a chave de upsert)."));
        }

        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            return Result<LogradouroComplemento>.Failure(new DomainError(
                GeoReferenceDataErrorCodes.LogradouroComplementoVersaoDatasetObrigatoria,
                "Versão do dataset (proveniência) do complemento é obrigatória."));
        }

        var entidade = new LogradouroComplemento
        {
            Cep = cep.Trim(),
            Complemento = complemento.Trim(),
            ComplementoNormalizado = GeoTexto.NormalizarTexto(complementoNormalizado),
            VersaoDataset = versaoDataset.Trim(),
            Vigente = vigente,
        };

        return Result<LogradouroComplemento>.Success(entidade);
    }
}
