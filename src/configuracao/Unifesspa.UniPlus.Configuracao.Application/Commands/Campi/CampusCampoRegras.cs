namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

/// <summary>
/// Regras de formato de campos opcionais do Campus compartilhadas entre os
/// validators de criação e atualização — mantém a fronteira de validação
/// simétrica (sigla/nome/cidade/cep/coordenadas todos antecipados no validator)
/// sem duplicar predicados. Os limites espelham os do agregado <c>Campus</c>.
/// </summary>
internal static class CampusCampoRegras
{
    public const decimal LatitudeMin = -90m;
    public const decimal LatitudeMax = 90m;
    public const decimal LongitudeMin = -180m;
    public const decimal LongitudeMax = 180m;

    private const int CepLength = 8;

    /// <summary>
    /// Indica se o CEP é válido (8 dígitos numéricos) ou ausente. Espelha a
    /// normalização do agregado (Trim) para não rejeitar um valor que o domínio
    /// aceitaria.
    /// </summary>
    public static bool CepValidoOuAusente(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep))
        {
            return true;
        }

        string normalizado = cep.Trim();
        return normalizado.Length == CepLength && normalizado.All(char.IsAsciiDigit);
    }
}
