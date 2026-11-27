namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os códigos de um lote que o cadastro pode ter: um código fora do formato do cadastro
/// nunca tem registro, então sai do lote em vez de virar consulta. Quem lê por código
/// compara pelo value object, e o lote chega sem repetição.
/// </summary>
internal static class CodigosDoLote
{
    internal static List<TCodigo> Validos<TCodigo>(
        IReadOnlyCollection<string> codigos,
        Func<string?, Result<TCodigo>> criar)
        where TCodigo : class
    {
        return [.. codigos
            .Select(criar)
            .Where(resultado => resultado.IsSuccess)
            .Select(resultado => resultado.Value!)
            .Distinct()];
    }

    /// <summary>
    /// Os códigos de texto livre de um lote, sem repetição. O caractere nulo não cabe em coluna de texto do
    /// Postgres e faria o parâmetro ser recusado: esse código não tem cadastro.
    /// </summary>
    internal static List<string> Textuais(IReadOnlyCollection<string> codigos) =>
        [.. codigos
            .Where(static codigo => !codigo.Contains('\0', StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)];
}
