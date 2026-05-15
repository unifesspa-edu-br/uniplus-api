namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.AddOrganizacaoInstitucionalInfrastructure.")]
internal sealed class AreaOrganizacionalRepository : IAreaOrganizacionalRepository
{
    private readonly OrganizacaoInstitucionalDbContext _dbContext;

    public AreaOrganizacionalRepository(OrganizacaoInstitucionalDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<AreaOrganizacional?> ObterPorCodigoAsync(AreaCodigo codigo, CancellationToken cancellationToken)
    {
        return _dbContext.AreasOrganizacionais
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Codigo == codigo, cancellationToken);
    }

    public Task<AreaOrganizacional?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.AreasOrganizacionais
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public Task<bool> ExistePorCodigoAsync(AreaCodigo codigo, CancellationToken cancellationToken)
    {
        return _dbContext.AreasOrganizacionais
            .AsNoTracking()
            .AnyAsync(a => a.Codigo == codigo, cancellationToken);
    }

    public async Task<IReadOnlyList<AreaOrganizacional>> ListarAtivasAsync(CancellationToken cancellationToken)
    {
        List<AreaOrganizacional> areas = await _dbContext.AreasOrganizacionais
            .AsNoTracking()
            .OrderBy(a => a.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return areas;
    }

    public async Task AdicionarAsync(AreaOrganizacional area, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(area);
        await _dbContext.AreasOrganizacionais.AddAsync(area, cancellationToken).ConfigureAwait(false);
    }
}
