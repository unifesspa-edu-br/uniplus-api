namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

public sealed class BaseLegalBonusRegionalReader : IBaseLegalBonusRegionalReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public BaseLegalBonusRegionalReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<BaseLegalBonusRegionalView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Domain.Entities.BaseLegalBonusRegional? entity = await _dbContext.BaseLegaisBonus
            .AsNoTracking()
            .Include(x => x.Municipios)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return null;

        return new BaseLegalBonusRegionalView(
            entity.Id,
            entity.TipoInstrumento.ToCodigo(),
            entity.Identificacao,
            entity.Descricao,
            entity.Municipios.Select(m => new BaseLegalBonusRegionalMunicipioView(m.CodigoIbge, m.Nome, m.Uf)).ToList().AsReadOnly());
    }
}
