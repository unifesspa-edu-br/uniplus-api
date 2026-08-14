namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Fábrica única do campo <c>type</c> do corpo de erro RFC 9457. Todo emissor de
/// <c>problem+json</c> do monólito — erro de domínio, 401/403, 406 de content negotiation
/// e o boundary de exceção — passa por aqui, para que a URI publicada seja uma só.
/// </summary>
public interface IProblemTypeUriFactory
{
    /// <summary>
    /// URI absoluta da página do catálogo público que explica <paramref name="code"/>.
    /// </summary>
    /// <param name="code">Código estável da causa (ex.: <c>uniplus.selecao.edital.nao_encontrado</c>).</param>
    string Build(string code);
}
