namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

/// <summary>
/// Invalida o cache do reader cross-módulo da <c>Instituicao</c> após operações
/// de escrita (ADR-0056). Chamada pós-commit, best-effort.
/// </summary>
public interface IInstituicaoCacheInvalidator
{
    Task InvalidarAsync(CancellationToken cancellationToken = default);
}
