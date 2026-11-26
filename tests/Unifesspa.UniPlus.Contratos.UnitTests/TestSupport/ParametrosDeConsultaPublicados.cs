namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using Microsoft.OpenApi;

/// <summary>Os parâmetros de query string de uma operação, do caminho ou da própria operação.</summary>
internal static class ParametrosDeConsultaPublicados
{
    /// <summary>O parâmetro da operação substitui o do caminho quando o nome e o local coincidem.</summary>
    public static IReadOnlyList<ParametroDeConsulta> De(IOpenApiPathItem caminho, OpenApiOperation operacao)
    {
        IList<IOpenApiParameter> daOperacao = operacao.Parameters ?? [];
        IEnumerable<IOpenApiParameter> doCaminho = (caminho.Parameters ?? [])
            .Where(parametro => !daOperacao.Any(propria => Coincidem(propria, parametro)));

        return [.. daOperacao.Concat(doCaminho)
            .Where(static parametro => parametro.In == ParameterLocation.Query && parametro.Name is not null)
            .Select(static parametro => new ParametroDeConsulta(parametro.Name!, TextoLivreNoContrato.Eh(parametro.Schema)))];
    }

    private static bool Coincidem(IOpenApiParameter a, IOpenApiParameter b) =>
        a.In == b.In && string.Equals(a.Name, b.Name, StringComparison.Ordinal);
}
