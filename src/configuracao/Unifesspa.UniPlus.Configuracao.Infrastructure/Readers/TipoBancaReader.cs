namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="ITipoBancaReader"/> (ADR-0056): leitura direta do
/// banco de Configuração (<c>AsNoTracking</c>, query filter de soft-delete por
/// convenção). Sem cache — o cadastro é de baixo volume e o congelamento por valor
/// no consumidor (ADR-0061) dispensa releitura quente. Ordena por <c>Codigo</c>
/// ascendente.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class TipoBancaReader : ITipoBancaReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoBancaReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TipoBancaView>> ListarVivosAsync(
        CancellationToken cancellationToken = default)
    {
        List<TipoBanca> entidades = await _dbContext.TiposBanca
            .AsNoTracking()
            .OrderBy(b => b.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<TipoBancaView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        TipoBanca? entidade = await _dbContext.TiposBanca
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static TipoBancaView ParaView(TipoBanca b) =>
        new(
            b.Id,
            b.Codigo.Valor,
            b.Nome,
            b.FaseTipica,
            b.Descricao);
}
