namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Implementação de <see cref="IModeloFormularioReader"/> (ADR-0056): leitura direta dos modelos
/// (<c>AsNoTracking</c>), na mesma forma da manutenção.
/// </summary>
internal sealed class ModeloFormularioReader : IModeloFormularioReader
{
    private readonly UniPlusDbContext _dbContext;

    public ModeloFormularioReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<ModeloFormularioView?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ModeloFormulario? modelo = await _dbContext.ModelosFormulario
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return modelo?.ToView();
    }
}
