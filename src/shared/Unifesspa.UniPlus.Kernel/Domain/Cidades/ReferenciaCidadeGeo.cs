namespace Unifesspa.UniPlus.Kernel.Domain.Cidades;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Kernel.Extensions;
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
    /// Mapa prefixo (2 primeiros dígitos do código IBGE) → UF, com a sigla e o nome. É a
    /// fonte de verdade do intervalo válido de prefixos (11–53, com lacunas), da coerência
    /// prefixo↔UF e dos nomes das UFs. Sem consultar o Geo.
    /// </summary>
    private static readonly FrozenDictionary<string, UnidadeFederativa> UfPorPrefixo = new Dictionary<string, UnidadeFederativa>(StringComparer.Ordinal)
    {
        ["11"] = new("RO", "Rondônia"),
        ["12"] = new("AC", "Acre"),
        ["13"] = new("AM", "Amazonas"),
        ["14"] = new("RR", "Roraima"),
        ["15"] = new("PA", "Pará"),
        ["16"] = new("AP", "Amapá"),
        ["17"] = new("TO", "Tocantins"),
        ["21"] = new("MA", "Maranhão"),
        ["22"] = new("PI", "Piauí"),
        ["23"] = new("CE", "Ceará"),
        ["24"] = new("RN", "Rio Grande do Norte"),
        ["25"] = new("PB", "Paraíba"),
        ["26"] = new("PE", "Pernambuco"),
        ["27"] = new("AL", "Alagoas"),
        ["28"] = new("SE", "Sergipe"),
        ["29"] = new("BA", "Bahia"),
        ["31"] = new("MG", "Minas Gerais"),
        ["32"] = new("ES", "Espírito Santo"),
        ["33"] = new("RJ", "Rio de Janeiro"),
        ["35"] = new("SP", "São Paulo"),
        ["41"] = new("PR", "Paraná"),
        ["42"] = new("SC", "Santa Catarina"),
        ["43"] = new("RS", "Rio Grande do Sul"),
        ["50"] = new("MS", "Mato Grosso do Sul"),
        ["51"] = new("MT", "Mato Grosso"),
        ["52"] = new("GO", "Goiás"),
        ["53"] = new("DF", "Distrito Federal"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// As 27 siglas de UF válidas (unidades federativas do Brasil), derivadas dos
    /// valores de <see cref="UfPorPrefixo"/> — mesma fonte de verdade, sem
    /// duplicar a lista.
    /// </summary>
    private static readonly FrozenSet<string> UfsValidas =
        UfPorPrefixo.Values.Select(static uf => uf.Sigla).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>As 27 siglas de UF válidas.</summary>
    public static IReadOnlyCollection<string> Ufs => UfsValidas;

    /// <summary>As 27 unidades federativas, com sigla e nome, em ordem alfabética do nome.</summary>
    public static IReadOnlyList<UnidadeFederativa> UnidadesFederativas { get; } =
        [.. UfPorPrefixo.Values.OrderBy(static uf => OrdemAlfabetica.Chave(uf.Nome), StringComparer.Ordinal)];

    /// <summary>
    /// A sigla da UF do município pelo prefixo do código IBGE; nula quando o código não tem a
    /// forma de código de município.
    /// </summary>
    public static string? UfDoCodigoIbge(string codigoIbge)
    {
        ArgumentNullException.ThrowIfNull(codigoIbge);
        return EhCodigoMunicipioValido(codigoIbge) ? UfPorPrefixo[codigoIbge[..2]].Sigla : null;
    }

    /// <summary>
    /// Indica se <paramref name="codigoIbge"/> tem a forma de um código IBGE de município: sete
    /// dígitos com prefixo de UF real. Validação só de formato, como a do resto desta referência.
    /// </summary>
    public static bool EhCodigoMunicipioValido(string codigoIbge)
    {
        ArgumentNullException.ThrowIfNull(codigoIbge);
        return codigoIbge.Length == CodigoIbgeLength && codigoIbge.All(char.IsAsciiDigit) && TemPrefixoDeUfValido(codigoIbge);
    }

    /// <summary>
    /// Indica se os dois primeiros dígitos de <paramref name="codigoIbge"/>
    /// (assumido já validado como 7 dígitos numéricos) correspondem a um prefixo
    /// de UF real. Útil para consumidores que só têm o código IBGE, sem
    /// <c>cidadeUf</c> declarada para cruzar coerência (ex.: cadastros que
    /// referenciam um município sem exibir nome/UF).
    /// </summary>
    public static bool TemPrefixoDeUfValido(string codigoIbge)
    {
        ArgumentNullException.ThrowIfNull(codigoIbge);
        return codigoIbge.Length >= 2 && UfPorPrefixo.ContainsKey(codigoIbge[..2]);
    }

    /// <summary>Indica se <paramref name="uf"/> é uma das 27 siglas de UF válidas (comparação exata, case-sensitive).</summary>
    public static bool EhUfValida(string uf)
    {
        ArgumentNullException.ThrowIfNull(uf);
        return UfsValidas.Contains(uf);
    }

    /// <summary>
    /// Valida a referência de cidade (formato + coerência de UF), acumulando
    /// toda violação independente em vez de parar na primeira — os três campos
    /// (código IBGE, nome, UF) ausentes ao mesmo tempo devolvem os três erros,
    /// não só um. Checagens dependentes (formato do código, coerência de UF com
    /// o prefixo) só rodam quando o campo do qual dependem já está presente,
    /// para não mascarar a causa raiz nem arriscar checar algo ausente. Retorna
    /// <see cref="Result.Success"/> quando não há nenhuma violação; caso
    /// contrário, um <see cref="Result"/> com um <see cref="FieldError"/> (campo
    /// não rotulado — quem chama mapeia por <see cref="DomainError.Code"/>, ex.:
    /// <c>Campus.CampoDaCidade</c>) por violação, na taxonomia de
    /// <see cref="CidadeReferenciaErrorCodes"/>.
    /// </summary>
    public static Result Validar(string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf)
    {
        List<FieldError> erros = [];

        bool codigoPresente = !string.IsNullOrWhiteSpace(cidadeCodigoIbge);
        if (!codigoPresente)
        {
            erros.Add(new(null, new DomainError(
                CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio,
                "Código IBGE da cidade é obrigatório.")));
        }

        if (string.IsNullOrWhiteSpace(cidadeNome))
        {
            erros.Add(new(null, new DomainError(
                CidadeReferenciaErrorCodes.NomeObrigatorio,
                "Nome da cidade é obrigatório.")));
        }
        else
        {
            string nome = cidadeNome.Trim();
            if (nome.Contains('\0'))
            {
                erros.Add(new(null, new DomainError(
                    CidadeReferenciaErrorCodes.NomeCaractereNulo,
                    "Nome da cidade não pode conter o caractere nulo (U+0000).")));
            }
            else if (nome.Length > NomeMaxLength)
            {
                erros.Add(new(null, new DomainError(
                    CidadeReferenciaErrorCodes.NomeTamanho,
                    $"Nome da cidade deve ter no máximo {NomeMaxLength} caracteres.")));
            }
        }

        bool ufPresente = !string.IsNullOrWhiteSpace(cidadeUf);
        if (!ufPresente)
        {
            erros.Add(new(null, new DomainError(
                CidadeReferenciaErrorCodes.UfObrigatoria,
                "UF da cidade é obrigatória.")));
        }

        // Coerência com o prefixo só faz sentido quando o código tem formato
        // válido — ufDoPrefixo permanece nulo (checagem abaixo pulada) quando o
        // código está ausente ou malformado.
        string? ufDoPrefixo = null;
        if (codigoPresente)
        {
            string codigo = cidadeCodigoIbge!.Trim();
            if (codigo.Length != CodigoIbgeLength || !codigo.All(char.IsAsciiDigit))
            {
                erros.Add(new(null, new DomainError(
                    CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido,
                    $"Código IBGE da cidade deve ter exatamente {CodigoIbgeLength} dígitos numéricos.")));
            }
            else if (UfPorPrefixo.GetValueOrDefault(codigo[..2])?.Sigla is not { } sigla)
            {
                erros.Add(new(null, new DomainError(
                    CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido,
                    "Os dois primeiros dígitos do código IBGE não correspondem a uma UF válida.")));
            }
            else
            {
                ufDoPrefixo = sigla;
            }
        }

        if (ufDoPrefixo is not null && ufPresente
            && !string.Equals(ufDoPrefixo, cidadeUf!.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            // Mensagem genérica de propósito (ADR-0023): nunca ecoar o dado
            // rejeitado — cidadeUf chega sem limite de tamanho validado até aqui.
            erros.Add(new(null, new DomainError(
                CidadeReferenciaErrorCodes.UfIncoerente,
                "A UF informada não corresponde à UF do código IBGE informado.")));
        }

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
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

/// <summary>Uma unidade federativa do Brasil: a sigla e o nome.</summary>
public sealed record UnidadeFederativa(string Sigla, string Nome);
