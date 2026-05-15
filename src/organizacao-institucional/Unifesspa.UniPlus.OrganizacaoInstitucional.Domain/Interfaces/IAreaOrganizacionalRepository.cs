namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

using Unifesspa.UniPlus.Governance.Contracts;
using Entities;

/// <summary>
/// Repositório de <see cref="AreaOrganizacional"/>. Conforme ADR-0042, a
/// Application consome esta abstração — NÃO depende diretamente do
/// <c>DbContext</c>.
/// </summary>
public interface IAreaOrganizacionalRepository
{
    Task<AreaOrganizacional?> ObterPorCodigoAsync(AreaCodigo codigo, CancellationToken cancellationToken);
    Task<AreaOrganizacional?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistePorCodigoAsync(AreaCodigo codigo, CancellationToken cancellationToken);
    Task<IReadOnlyList<AreaOrganizacional>> ListarAtivasAsync(CancellationToken cancellationToken);
    Task AdicionarAsync(AreaOrganizacional area, CancellationToken cancellationToken);
}
