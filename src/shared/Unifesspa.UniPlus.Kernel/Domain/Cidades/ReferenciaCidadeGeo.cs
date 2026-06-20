namespace Unifesspa.UniPlus.Kernel.Domain.Cidades;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Validação server-side da referência de cidade do módulo <c>Geo</c>
/// (ADR-0090). Entidades de outros módulos (<c>Campus</c>, <c>LocalOferta</c>,
/// <c>Instituicao</c>) guardam <c>cidade_codigo_ibge</c> (código IBGE de 7
/// dígitos) + display cache (<c>cidade_nome</c>, <c>cidade_uf</c>) preenchido
/// pelo frontend a partir da API do Geo — composição no cliente, sem FK
/// cross-banco nem chamada ao Geo.
/// </summary>
/// <remarks>
/// <para>A validação é apenas de <strong>formato</strong>: 7 dígitos numéricos,
/// prefixo de UF (2 primeiros dígitos) coerente com a <c>cidade_uf</c> informada,
/// e <c>cidade_nome</c> não-vazio. <strong>Não</strong> há verificação de dígito
/// verificador (evita depender de algoritmo de DV não-padronizado, risco de
/// falso-negativo) nem consulta ao Geo. A existência real da cidade fica a cargo
/// do front (que só oferece cidades reais) + reconciliação eventual.</para>
/// <para>Este padrão de referência fraca vale só para dado público estável
/// (município IBGE) — nunca para invariante de autorização/elegibilidade/
/// financeiro/legal.</para>
/// </remarks>
public static class ReferenciaCidadeGeo
{
    /// <summary>Comprimento exato do código IBGE de município (7 dígitos).</summary>
    public const int CodigoIbgeLength = 7;

    /// <summary>Comprimento da sigla de UF (2 letras).</summary>
    public const int UfLength = 2;

    /// <summary>Comprimento máximo do nome de cidade no display cache.</summary>
    public const int NomeMaxLength = 150;

    /// <summary>Comprimento máximo da proveniência do display cache.</summary>
    public const int OrigemMaxLength = 50;

    /// <summary>Proveniência padrão do display cache: composição no cliente sobre a API do Geo.</summary>
    public const string OrigemGeoApi = "geo-api";

    /// <summary>
    /// Mapa prefixo (2 primeiros dígitos do código IBGE) → sigla da UF. É a fonte
    /// de verdade do intervalo válido de prefixos (11–53, com lacunas) e da
    /// coerência prefixo↔UF. Sem consultar o Geo.
    /// </summary>
    private static readonly FrozenDictionary<string, string> UfPorPrefixo = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["11"] = "RO",
        ["12"] = "AC",
        ["13"] = "AM",
        ["14"] = "RR",
        ["15"] = "PA",
        ["16"] = "AP",
        ["17"] = "TO",
        ["21"] = "MA",
        ["22"] = "PI",
        ["23"] = "CE",
        ["24"] = "RN",
        ["25"] = "PB",
        ["26"] = "PE",
        ["27"] = "AL",
        ["28"] = "SE",
        ["29"] = "BA",
        ["31"] = "MG",
        ["32"] = "ES",
        ["33"] = "RJ",
        ["35"] = "SP",
        ["41"] = "PR",
        ["42"] = "SC",
        ["43"] = "RS",
        ["50"] = "MS",
        ["51"] = "MT",
        ["52"] = "GO",
        ["53"] = "DF",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Valida a referência de cidade (formato + coerência de UF). Retorna
    /// <see cref="Result.Success"/> quando o código tem 7 dígitos numéricos com
    /// prefixo de UF coerente com <paramref name="cidadeUf"/> e
    /// <paramref name="cidadeNome"/> não-vazio; caso contrário, um
    /// <see cref="DomainError"/> com o código apropriado de
    /// <see cref="CidadeReferenciaErrorCodes"/>.
    /// </summary>
    public static Result Validar(string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf)
    {
        if (string.IsNullOrWhiteSpace(cidadeCodigoIbge))
        {
            return Result.Failure(new DomainError(
                CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio,
                "Código IBGE da cidade é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(cidadeNome))
        {
            return Result.Failure(new DomainError(
                CidadeReferenciaErrorCodes.NomeObrigatorio,
                "Nome da cidade é obrigatório."));
        }

        if (cidadeNome.Trim().Length > NomeMaxLength)
        {
            return Result.Failure(new DomainError(
                CidadeReferenciaErrorCodes.NomeTamanho,
                $"Nome da cidade deve ter no máximo {NomeMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(cidadeUf))
        {
            return Result.Failure(new DomainError(
                CidadeReferenciaErrorCodes.UfObrigatoria,
                "UF da cidade é obrigatória."));
        }

        string codigo = cidadeCodigoIbge.Trim();
        if (codigo.Length != CodigoIbgeLength || !codigo.All(char.IsAsciiDigit))
        {
            return Result.Failure(new DomainError(
                CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido,
                $"Código IBGE da cidade deve ter exatamente {CodigoIbgeLength} dígitos numéricos."));
        }

        string prefixo = codigo[..2];
        if (!UfPorPrefixo.TryGetValue(prefixo, out string? ufDoPrefixo))
        {
            return Result.Failure(new DomainError(
                CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido,
                $"Os dois primeiros dígitos do código IBGE ('{prefixo}') não correspondem a uma UF válida."));
        }

        if (!string.Equals(ufDoPrefixo, cidadeUf.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new DomainError(
                CidadeReferenciaErrorCodes.UfIncoerente,
                $"O prefixo do código IBGE ('{prefixo}') corresponde à UF '{ufDoPrefixo}', "
                + $"incompatível com a UF informada ('{cidadeUf.Trim()}')."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Predicado conveniente (sem propagar <see cref="DomainError"/>) para uso em
    /// validators FluentValidation: indica se a referência tem formato e UF
    /// coerentes. No caminho de falha o <see cref="Validar"/> subjacente ainda
    /// instancia o erro, que é descartado aqui.
    /// </summary>
    public static bool EhValida(string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf) =>
        Validar(cidadeCodigoIbge, cidadeNome, cidadeUf).IsSuccess;
}
