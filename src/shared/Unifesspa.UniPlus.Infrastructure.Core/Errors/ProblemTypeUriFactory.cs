namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.Extensions.Options;

/// <summary>
/// Concatena o <c>code</c> à base configurada em <see cref="ProblemTypeOptions"/>,
/// normalizando a barra final uma única vez, na construção.
/// </summary>
internal sealed class ProblemTypeUriFactory : IProblemTypeUriFactory
{
    private readonly string _baseUri;

    public ProblemTypeUriFactory(IOptions<ProblemTypeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _baseUri = NormalizarBase(options.Value.BaseUri);
    }

    public string Build(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return _baseUri + code;
    }

    /// <summary>
    /// Garante exatamente uma barra entre a base e o código. Configuração escrita sem
    /// barra final, com uma ou com várias produz a mesma URI — o consumidor recebe um
    /// valor só, e um erro de digitação na configuração não vira caminho com barra
    /// duplicada, que o catálogo não resolve.
    /// </summary>
    internal static string NormalizarBase(string baseUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUri);

        return baseUri.Trim().TrimEnd('/') + "/";
    }
}
