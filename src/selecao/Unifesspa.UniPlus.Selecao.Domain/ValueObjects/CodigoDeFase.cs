namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O código canônico de fase como o processo o guarda: texto em branco e ausência são o mesmo estado, e o
/// código é aparado. Sem isso, a mesma configuração geraria bytes canônicos distintos conforme o cliente
/// enviasse nulo ou texto vazio.
/// </summary>
public static class CodigoDeFase
{
    public static string? Normalizado(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
}
