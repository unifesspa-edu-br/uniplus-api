namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="TurnoOferta"/> (domínio, PascalCase) e o token
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>):
/// só os quatro tokens canônicos são aceitos — sem <c>Enum.TryParse</c> (que
/// aceitaria tokens numéricos e nomes PascalCase, fora do contrato da #749).</para>
/// <para>É o vocabulário fonte do CHECK de domínio em <c>oferta_curso.turno</c>
/// (<see cref="TokensCanonicos"/>, coluna anulável) e do value converter de
/// persistência. A ausência de turno (nulo) é decidida pela entidade, não aqui.</para>
/// </remarks>
public static class TurnosOferta
{
    private static readonly Dictionary<TurnoOferta, string> ParaToken = new()
    {
        [TurnoOferta.Matutino] = "MATUTINO",
        [TurnoOferta.Vespertino] = "VESPERTINO",
        [TurnoOferta.Noturno] = "NOTURNO",
        [TurnoOferta.Integral] = "INTEGRAL",
    };

    private static readonly Dictionary<string, TurnoOferta> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os quatro tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um turno válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="turno"/> é <see cref="TurnoOferta.Nenhum"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(TurnoOferta turno) =>
        ParaToken.TryGetValue(turno, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(turno), turno, "Turno de oferta fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) ao turno correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens numéricos ou fora do
    /// domínio (allowlist). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out TurnoOferta turno)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out TurnoOferta resolvido))
        {
            turno = resolvido;
            return true;
        }

        turno = TurnoOferta.Nenhum;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos quatro tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
