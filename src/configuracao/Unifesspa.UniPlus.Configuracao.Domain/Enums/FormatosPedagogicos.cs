namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="FormatoPedagogico"/> (domínio, PascalCase) e o
/// token textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>):
/// só os três tokens canônicos são aceitos — sem <c>Enum.TryParse</c> (que
/// aceitaria tokens numéricos e nomes PascalCase, fora do contrato da #749).</para>
/// <para>É o vocabulário fonte do CHECK de domínio em
/// <c>oferta_curso.formato_pedagogico</c> (<see cref="TokensCanonicos"/>) e do
/// value converter de persistência. O default quando o token está ausente
/// (PRESENCIAL) é aplicado pela entidade, não aqui.</para>
/// </remarks>
public static class FormatosPedagogicos
{
    private static readonly Dictionary<FormatoPedagogico, string> ParaToken = new()
    {
        [FormatoPedagogico.Presencial] = "PRESENCIAL",
        [FormatoPedagogico.Semipresencial] = "SEMIPRESENCIAL",
        [FormatoPedagogico.Ead] = "EAD",
    };

    private static readonly Dictionary<string, FormatoPedagogico> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os três tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um formato válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="formato"/> é <see cref="FormatoPedagogico.Nenhum"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(FormatoPedagogico formato) =>
        ParaToken.TryGetValue(formato, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(formato), formato, "Formato pedagógico fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) ao formato correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens numéricos ou fora do
    /// domínio (allowlist). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out FormatoPedagogico formato)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out FormatoPedagogico resolvido))
        {
            formato = resolvido;
            return true;
        }

        formato = FormatoPedagogico.Nenhum;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos três tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
