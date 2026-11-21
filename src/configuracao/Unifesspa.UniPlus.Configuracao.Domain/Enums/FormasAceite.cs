namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="FormaAceite"/> (domínio, PascalCase) e o código
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// Parsing por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>) — não usa
/// <c>Enum.TryParse</c>, que aceitaria códigos numéricos e nomes PascalCase fora do
/// contrato textual.
/// </remarks>
public static class FormasAceite
{
    private static readonly Dictionary<FormaAceite, string> ParaToken = new()
    {
        [FormaAceite.ADefinir] = "A_DEFINIR",
        [FormaAceite.RegistroDigitalSemLogIp] = "REGISTRO_DIGITAL_SEM_LOG_IP",
        [FormaAceite.RegistroDigitalComLogIp] = "REGISTRO_DIGITAL_COM_LOG_IP",
    };

    private static readonly Dictionary<string, FormaAceite> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os três códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de uma forma de aceite válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="formaAceite"/> é <see cref="FormaAceite.Nenhuma"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(FormaAceite formaAceite) =>
        ParaToken.TryGetValue(formaAceite, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(formaAceite), formaAceite, "Forma de aceite fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) à forma de aceite correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos fora do domínio (lista de códigos aceitos).
    /// Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out FormaAceite formaAceite)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out FormaAceite resolvida))
        {
            formaAceite = resolvida;
            return true;
        }

        formaAceite = FormaAceite.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);
}
