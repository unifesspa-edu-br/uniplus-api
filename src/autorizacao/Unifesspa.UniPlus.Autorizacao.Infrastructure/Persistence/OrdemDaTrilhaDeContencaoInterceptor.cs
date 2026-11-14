namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Dá a ordem total da trilha da contenção aos registros novos de cada gravação: ordena pelo ordinal em que o agregado os
/// criou e atribui o próximo número da sequence compartilhada a cada um. Como toda transição de contenção passa pela trava
/// do perfil antes de criar registro, a transição seguinte do mesmo perfil só chega à sequence depois do commit da anterior,
/// e os números saem em ordem causal mesmo com o mesmo instante. Lacunas da sequence não importam: a ordem é o que conta.
/// </summary>
public sealed class OrdemDaTrilhaDeContencaoInterceptor : SaveChangesInterceptor
{
    private const string ProximoNumero =
        $"SELECT nextval('{AutorizacaoContribuicao.NomeDoEsquema}.{AutorizacaoContribuicao.SequenciaDaOrdemDaTrilha}') AS \"Value\"";

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            foreach (EntityEntry registro in NovosRegistros(context))
            {
                Numerar(registro, context.Database.SqlQueryRaw<long>(ProximoNumero).AsEnumerable().Single());
            }
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            foreach (EntityEntry registro in NovosRegistros(context))
            {
                Numerar(registro, await context.Database.SqlQueryRaw<long>(ProximoNumero).SingleAsync(cancellationToken).ConfigureAwait(false));
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private static List<EntityEntry> NovosRegistros(DbContext context) =>
        [.. context.ChangeTracker.Entries()
            .Where(static e => e is { State: EntityState.Added, Entity: IRegistroDaTrilhaDeContencao })
            .OrderBy(static e => ((IRegistroDaTrilhaDeContencao)e.Entity).OrdinalLocal)];

    private static void Numerar(EntityEntry registro, long numero) =>
        registro.Property(nameof(IRegistroDaTrilhaDeContencao.OrdemNaTrilha)).CurrentValue = numero;
}
