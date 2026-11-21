namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="FormatoPedagogico"/> (domínio, PascalCase) e o
/// código textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>):
/// só os três códigos canônicos são aceitos — sem <c>Enum.TryParse</c> (que
/// aceitaria códigos numéricos e nomes PascalCase, fora do contrato da #749).</para>
/// <para>É o vocabulário fonte do CHECK de domínio em
/// <c>ofertas_de_curso.formato_pedagogico</c> (<see cref="TokensCanonicos"/>) e do
/// value converter de persistência. O default quando o código está ausente
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

    /// <summary>Os três códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de um formato válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="formato"/> é <see cref="FormatoPedagogico.Nenhum"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(FormatoPedagogico formato) =>
        ParaToken.TryGetValue(formato, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(formato), formato, "Formato pedagógico fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) ao formato correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos numéricos ou fora do
    /// domínio (lista de códigos aceitos). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out FormatoPedagogico formato)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out FormatoPedagogico resolvido))
        {
            formato = resolvido;
            return true;
        }

        formato = FormatoPedagogico.Nenhum;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos três códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);
}
