namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em OrganizacaoInstitucionalInfrastructureRegistration.")]
public sealed class InstituicaoRepository : IInstituicaoRepository
{
    private readonly OrganizacaoInstitucionalDbContext _dbContext;

    public InstituicaoRepository(OrganizacaoInstitucionalDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Instituicao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Instituicoes
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public Task<Instituicao?> ObterParaLeituraAsync(CancellationToken cancellationToken)
    {
        // Query filter exclui soft-deleted; sob o singleton há no máximo uma viva.
        return _dbContext.Instituicoes
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Instituicao instituicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instituicao);
        await _dbContext.Instituicoes.AddAsync(instituicao, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(Instituicao instituicao)
    {
        ArgumentNullException.ThrowIfNull(instituicao);
        _dbContext.Instituicoes.Remove(instituicao);
    }

    public Task<bool> ExisteAlgumaVivaAsync(CancellationToken cancellationToken)
    {
        // Query filter exclui soft-deleted ⇒ AnyAsync reflete apenas registros vivos.
        return _dbContext.Instituicoes
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public Task<bool> ExisteComUnidadeRaizAsync(Guid unidadeId, CancellationToken cancellationToken)
    {
        return _dbContext.Instituicoes
            .AsNoTracking()
            .AnyAsync(i => i.UnidadeRaizId == unidadeId, cancellationToken);
    }
}
