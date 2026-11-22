namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Implementação de <see cref="ICalendarioVigenteReader"/> (ADR-0056): leitura
/// direta do banco de Configuração (<c>AsNoTracking</c>, query filter de
/// soft-delete por convenção). Sem cache — troca de vigente é rara e o
/// congelamento por valor no consumidor (ADR-0061) dispensa releitura quente.
/// </summary>
internal sealed class CalendarioVigenteReader : ICalendarioVigenteReader
{
    private readonly UniPlusDbContext _dbContext;

    public CalendarioVigenteReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<CalendarioVigenteView?> ObterVigenteAsync(CancellationToken cancellationToken = default)
    {
        CalendarioDiasUteis? calendario = await _dbContext.CalendariosDiasUteis
            .AsNoTracking()
            .Include(c => c.DiasNaoUteis)
            .FirstOrDefaultAsync(c => c.Vigente, cancellationToken)
            .ConfigureAwait(false);

        if (calendario is null)
        {
            return null;
        }

        DiaNaoUtilView[] dias = [.. calendario.DiasNaoUteis.Select(ParaView)];
        return new CalendarioVigenteView(calendario.Id, calendario.VersaoDataset, dias);
    }

    private static DiaNaoUtilView ParaView(DiaNaoUtil dia) =>
        new(
            dia.Data,
            dia.Abrangencia.Codigo(),
            dia.MunicipioIbge,
            dia.MunicipioNome,
            dia.MunicipioUf,
            dia.Uf);
}
