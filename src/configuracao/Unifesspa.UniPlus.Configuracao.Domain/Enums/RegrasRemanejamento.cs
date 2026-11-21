namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="RegraRemanejamento"/> (domínio, PascalCase) e o
/// código textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>):
/// só os três códigos canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria códigos numéricos e nomes PascalCase do enum
/// — ambos fora do contrato textual da #589.</para>
/// <para>É o vocabulário fonte do CHECK de domínio (null-safe) em
/// <c>modalidade.regra_remanejamento</c> (<see cref="TokensCanonicos"/>) e do value
/// converter de persistência.</para>
/// </remarks>
public static class RegrasRemanejamento
{
    private static readonly Dictionary<RegraRemanejamento, string> ParaToken = new()
    {
        [RegraRemanejamento.SegueCascata] = "SEGUE_CASCATA",
        [RegraRemanejamento.DestinoUnico] = "DESTINO_UNICO",
        [RegraRemanejamento.Cruzado] = "CRUZADO",
    };

    private static readonly Dictionary<string, RegraRemanejamento> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os três códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de uma regra válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regra"/> é <see cref="RegraRemanejamento.Nenhuma"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(RegraRemanejamento regra) =>
        ParaToken.TryGetValue(regra, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(regra), regra, "Regra de remanejamento fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) à regra correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos numéricos ou fora do
    /// domínio (lista de códigos aceitos). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out RegraRemanejamento regra)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out RegraRemanejamento resolvida))
        {
            regra = resolvida;
            return true;
        }

        regra = RegraRemanejamento.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos três códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);
}
