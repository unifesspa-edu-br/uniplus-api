namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

/// <summary>
/// Invalida o cache do reader cross-módulo de <c>Unidade</c> após operações
/// de escrita (ADR-0056). Chamada pós-commit, best-effort.
/// </summary>
public interface IUnidadeCacheInvalidator
{
    Task InvalidarAsync(CancellationToken cancellationToken = default);
}
