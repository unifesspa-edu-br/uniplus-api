namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Implementação de <see cref="IReferenciaReservaDemograficaReader"/> (ADR-0056):
/// leitura direta do banco de Configuração (<c>AsNoTracking</c>, query filter de
/// soft-delete por convenção). Sem cache — o cadastro é de baixo volume e baixa
/// frequência de leitura viva; o congelamento por valor no consumidor (ADR-0061)
/// dispensa releitura quente.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
internal sealed class ReferenciaReservaDemograficaReader : IReferenciaReservaDemograficaReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public ReferenciaReservaDemograficaReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ReferenciaReservaDemograficaView>> ListarVivasAsync(
        CancellationToken cancellationToken = default)
    {
        List<ReferenciaReservaDemografica> entidades = await _dbContext.ReferenciasReservaDemografica
            .AsNoTracking()
            .OrderBy(r => r.CensoReferencia)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entidades.Select(ParaView)];
    }

    public async Task<ReferenciaReservaDemograficaView?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ReferenciaReservaDemografica? entidade = await _dbContext.ReferenciasReservaDemografica
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entidade is null ? null : ParaView(entidade);
    }

    private static ReferenciaReservaDemograficaView ParaView(ReferenciaReservaDemografica r) =>
        new(
            r.Id,
            r.CensoReferencia,
            r.PpiPercentual.Valor,
            r.QuilombolaPercentual.Valor,
            r.PcdPercentual.Valor,
            r.BaseLegal);
}
