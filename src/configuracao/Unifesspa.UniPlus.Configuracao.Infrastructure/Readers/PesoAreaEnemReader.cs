namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IPesoAreaEnemReader"/> (ADR-0056): leitura direta
/// do banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e baixa frequência de
/// leitura viva; o congelamento por valor no consumidor (ADR-0061) dispensa
/// releitura quente.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class PesoAreaEnemReader : IPesoAreaEnemReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public PesoAreaEnemReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PesoAreaEnemView>> ListarVivasAsync(
        CancellationToken cancellationToken = default)
    {
        List<PesoAreaEnem> entidades = await _dbContext.PesosAreaEnem
            .AsNoTracking()
            .OrderBy(p => p.Resolucao)
            .ThenBy(p => p.GrupoCurso)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<PesoAreaEnemView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        PesoAreaEnem? entidade = await _dbContext.PesosAreaEnem
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static PesoAreaEnemView ParaView(PesoAreaEnem p) =>
        new(
            p.Id,
            p.Resolucao,
            p.GrupoCurso.Valor,
            p.PesoRedacao,
            p.PesoCienciasNatureza,
            p.PesoCienciasHumanas,
            p.PesoLinguagens,
            p.PesoMatematica,
            p.CorteRedacao,
            p.BaseLegal);
}
