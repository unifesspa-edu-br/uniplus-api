namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Implementação de <see cref="IModeloFormularioReader"/> (ADR-0056): leitura direta dos modelos
/// (<c>AsNoTracking</c>), na mesma forma da manutenção.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class ModeloFormularioReader : IModeloFormularioReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public ModeloFormularioReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ModeloFormularioView>> ListarAtivosAsync(
        string tipoProcessoCodigo, string finalidade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tipoProcessoCodigo);
        FinalidadeFormulario daFinalidade = EstruturaFormulario.FinalidadeDoToken(finalidade);
        if (daFinalidade == FinalidadeFormulario.Nenhuma)
        {
            return [];
        }

        string tipo = tipoProcessoCodigo.Trim();
        List<ModeloFormulario> modelos = await _dbContext.ModelosFormulario
            .AsNoTracking()
            .Where(m => m.Ativo && m.Finalidade == daFinalidade && (m.TipoProcessoCodigo == null || m.TipoProcessoCodigo == tipo))
            .OrderBy(m => m.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. modelos.Select(static m => m.ToView())];
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
