namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IPrecedenciaFaseReader"/> (ADR-0056): leitura
/// direta do banco de Configuração (<c>AsNoTracking</c>, query filter de
/// soft-delete por convenção). Sem cache — o cadastro é de baixo volume.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class PrecedenciaFaseReader : IPrecedenciaFaseReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public PrecedenciaFaseReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PrecedenciaFaseView>> ListarVivasAsync(
        CancellationToken cancellationToken = default)
    {
        List<PrecedenciaFase> entidades = await _dbContext.PrecedenciasFase
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<PrecedenciaFaseView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        PrecedenciaFase? entidade = await _dbContext.PrecedenciasFase
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static PrecedenciaFaseView ParaView(PrecedenciaFase p) =>
        new(p.Id, p.AntecessoraCodigo, p.SucessoraCodigo, p.PermiteSobreposicao);
}
