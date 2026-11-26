namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

/// <summary>Monta, para as descrições de cadastro sem código complementar, o que o cadastro encontrou para os códigos pedidos.</summary>
internal static class AtributosDosCodigos
{
    public static string[] Textos(IReadOnlyCollection<(string Codigo, string? Complemento)> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        return [.. codigos.Select(static codigo => codigo.Codigo).Distinct(StringComparer.Ordinal)];
    }

    public static IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> DosPedidos<T>(
        IEnumerable<T> cadastro,
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos,
        Func<T, string> codigoDe,
        Func<T, IReadOnlyList<object?>> atributosDe)
    {
        HashSet<string> pedidos = [.. Textos(codigos)];
        return cadastro.Where(item => pedidos.Contains(codigoDe(item)))
            .ToDictionary(item => (codigoDe(item), (string?)null), atributosDe);
    }
}
