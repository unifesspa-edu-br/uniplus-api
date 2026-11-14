namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="NaturezaConceito"/> (domínio), o token do banco (UPPER_SNAKE) e o nome
/// no contrato REST (camelCase), com parsing de domínio fechado por allowlist textual — sem
/// <c>Enum.TryParse</c>, que aceitaria números e nomes fora do contrato.
/// </summary>
public static class NaturezasConceito
{
    private sealed record Nomeacao(NaturezaConceito Natureza, string Token, string Contrato);

    private static readonly Nomeacao[] Roster =
    [
        new(NaturezaConceito.Numerico, "NUMERICO", "numerico"),
        new(NaturezaConceito.Categorico, "CATEGORICO", "categorico"),
    ];

    /// <summary>Os tokens do banco, para o CHECK de domínio.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. Roster.Select(static n => n.Token)];

    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="natureza"/> é <see cref="NaturezaConceito.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaToken(NaturezaConceito natureza) => Nomear(natureza).Token;

    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="natureza"/> é <see cref="NaturezaConceito.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaContrato(NaturezaConceito natureza) => Nomear(natureza).Contrato;

    /// <summary>Resolve o token do banco; sensível a caixa.</summary>
    public static bool TryAnalisarToken(string? token, out NaturezaConceito natureza) =>
        TryAnalisar(n => string.Equals(n.Token, token, StringComparison.Ordinal), out natureza);

    /// <summary>Resolve o nome no contrato REST; sensível a caixa.</summary>
    public static bool TryAnalisarContrato(string? contrato, out NaturezaConceito natureza) =>
        TryAnalisar(n => string.Equals(n.Contrato, contrato, StringComparison.Ordinal), out natureza);

    private static bool TryAnalisar(Func<Nomeacao, bool> corresponde, out NaturezaConceito natureza)
    {
        Nomeacao? achada = Array.Find(Roster, n => corresponde(n));
        natureza = achada?.Natureza ?? NaturezaConceito.Nenhuma;
        return achada is not null;
    }

    private static Nomeacao Nomear(NaturezaConceito natureza) =>
        Array.Find(Roster, n => n.Natureza == natureza)
        ?? throw new ArgumentOutOfRangeException(nameof(natureza), natureza, "Natureza de conceito fora do domínio fechado.");
}
