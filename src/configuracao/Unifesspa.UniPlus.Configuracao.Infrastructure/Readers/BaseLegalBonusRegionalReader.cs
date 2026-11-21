namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Implementação de <see cref="IBaseLegalBonusRegionalReader"/> (ADR-0056): leitura
/// direta do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete
/// por convenção).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em DependencyInjection.")]
internal sealed class BaseLegalBonusRegionalReader : IBaseLegalBonusRegionalReader
{
    private readonly UniPlusDbContext _dbContext;

    public BaseLegalBonusRegionalReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<BaseLegalBonusRegionalView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Domain.Entities.BaseLegalBonusRegional? entity = await ComMunicipios()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Visao(entity);
    }

    public async Task<BaseLegalBonusRegionalView?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        Result<CodigoBaseLegalBonusRegional> formado = CodigoBaseLegalBonusRegional.Criar(codigo);
        if (formado.IsFailure)
        {
            return null;
        }

        Domain.Entities.BaseLegalBonusRegional? entity = await ComMunicipios()
            .FirstOrDefaultAsync(x => x.Codigo == formado.Value, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Visao(entity);
    }

    private IQueryable<Domain.Entities.BaseLegalBonusRegional> ComMunicipios() =>
        _dbContext.BaseLegaisBonus.AsNoTracking().Include(x => x.Municipios);

    private static BaseLegalBonusRegionalView Visao(Domain.Entities.BaseLegalBonusRegional entity) => new(
        entity.Id,
        entity.Codigo.Valor,
        entity.TipoInstrumento.Codigo(),
        entity.Identificacao,
        entity.Descricao,
        entity.Municipios.Select(m => new BaseLegalBonusRegionalMunicipioView(m.CodigoIbge, m.Nome, m.Uf)).ToList().AsReadOnly());
}
