namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="CadastroDoCatalogo"/> e o token textual de contrato/banco (UPPER_SNAKE),
/// com parsing de domínio fechado por allowlist explícita (molde de <see cref="SituacoesDaAprovacao"/>).
/// </summary>
public static class CadastrosDoCatalogo
{
    private static readonly Dictionary<CadastroDoCatalogo, string> ParaToken = new()
    {
        [CadastroDoCatalogo.TipoDocumento] = "TIPO_DOCUMENTO",
        [CadastroDoCatalogo.Fato] = "FATO",
    };

    private static readonly Dictionary<string, CadastroDoCatalogo> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco de um cadastro válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="cadastro"/> é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(CadastroDoCatalogo cadastro) =>
        ParaToken.TryGetValue(cadastro, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(cadastro), cadastro, "Cadastro do catálogo fora do domínio fechado.");

    /// <summary>Resolve um token textual ao cadastro; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out CadastroDoCatalogo cadastro)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out CadastroDoCatalogo resolvido))
        {
            cadastro = resolvido;
            return true;
        }

        cadastro = CadastroDoCatalogo.Nenhum;
        return false;
    }

    /// <summary>Resolve um token ao seu enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static CadastroDoCatalogo Analisar(string? token) =>
        TryAnalisar(token, out CadastroDoCatalogo cadastro)
            ? cadastro
            : throw new ArgumentOutOfRangeException(
                nameof(token), token, "Token de cadastro do catálogo fora do domínio fechado.");
}
