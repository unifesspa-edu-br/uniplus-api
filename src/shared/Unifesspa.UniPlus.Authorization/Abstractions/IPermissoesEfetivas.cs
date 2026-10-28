namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Catalogo;

/// <summary>
/// Permissões do catálogo que o solicitante da requisição corrente possui. Serve a
/// interface (menu, ações visíveis); a decisão de acesso continua sendo tomada em cada
/// requisição, porque a concessão pode depender do recurso (ADR-0078).
/// </summary>
public interface IPermissoesEfetivas
{
    /// <summary>
    /// Permissões do catálogo cobertas por alguma concessão vigente do solicitante, ou
    /// <see langword="null"/> quando o token não o identifica por completo.
    /// </summary>
    Task<IReadOnlyList<DefinicaoDePermissao>?> ObterAsync(CancellationToken cancellationToken = default);
}
