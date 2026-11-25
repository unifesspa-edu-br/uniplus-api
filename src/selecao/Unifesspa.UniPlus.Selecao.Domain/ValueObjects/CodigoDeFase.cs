namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O código de fase preserva o valor informado; texto em branco e ausência são o mesmo estado.
/// </summary>
public static class CodigoDeFase
{
    public static string? Normalizado(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo;
}
