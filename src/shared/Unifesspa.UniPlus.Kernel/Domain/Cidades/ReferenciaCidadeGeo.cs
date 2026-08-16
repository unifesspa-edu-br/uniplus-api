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
    /// As 27 siglas de UF válidas (unidades federativas do Brasil), derivadas dos
    /// valores de <see cref="UfPorPrefixo"/> — mesma fonte de verdade, sem
    /// duplicar a lista.
    /// </summary>
    private static readonly FrozenSet<string> UfsValidas =
        UfPorPrefixo.Values.ToFrozenSet(StringComparer.Ordinal);

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
            else if (!UfPorPrefixo.TryGetValue(codigo[..2], out ufDoPrefixo))
            {
                erros.Add(new(null, new DomainError(
                    CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido,
                    "Os dois primeiros dígitos do código IBGE não correspondem a uma UF válida.")));
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
