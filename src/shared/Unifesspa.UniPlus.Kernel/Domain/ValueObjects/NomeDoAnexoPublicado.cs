namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// A forma do nome de um anexo pela qual dois anexos do mesmo ato são o mesmo: o nome sem os
/// espaços das pontas, comparado caractere a caractere.
/// </summary>
/// <remarks>
/// Vive no Kernel porque o módulo que monta o ato antes de enviá-lo e o módulo que o registra
/// precisam decidir igual quando dois nomes coincidem: se divergissem, o primeiro aceitaria uma
/// lista que o segundo recusa, depois de o pedido já ter sido respondido.
/// </remarks>
public static class NomeDoAnexoPublicado
{
    /// <summary>O comparador com que os nomes normalizados são considerados iguais.</summary>
    public static StringComparer Comparador => StringComparer.Ordinal;

    /// <summary>O nome sem os espaços das pontas; vazio quando ausente.</summary>
    public static string Normalizar(string? nome) => nome?.Trim() ?? string.Empty;
}
