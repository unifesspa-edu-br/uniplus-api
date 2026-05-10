namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Kernel.Domain.Entities;

// LGPD audit trail (issue #127): converte DELETE em UPDATE preservando o
// identificador do usuário responsável em DeletedBy. Em requests autenticados,
// o IUserContext (sub claim do JWT) é usado; em jobs, migrations e qualquer
// fluxo sem principal autenticado, cai para "system" como fallback explícito.
//
// Registrado como Scoped na DI dos módulos para acompanhar o ciclo de vida
// scoped do IUserContext (HttpUserContext) e do DbContext — evitar Singleton
// neste interceptor é deliberado: capturar um IUserContext scoped em um
// singleton causaria captive dependency e o UserId congelaria no primeiro
// request servido pelo processo.
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private const string SystemUser = "system";

    private readonly IUserContext? _userContext;

    // Construtor sem args preservado para uso em testes que não exigem
    // IUserContext (cenário equivalente ao fallback "system").
    public SoftDeleteInterceptor(IUserContext? userContext = null)
    {
        _userContext = userContext;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData?.Context is not null)
        {
            ConvertDeleteToSoftDelete(eventData.Context);
        }

        return base.SavingChangesAsync(eventData!, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData?.Context is not null)
        {
            ConvertDeleteToSoftDelete(eventData.Context);
        }

        return base.SavingChanges(eventData!, result);
    }

    private void ConvertDeleteToSoftDelete(DbContext context)
    {
        string deletedBy = ResolveDeletedBy();

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<EntityBase> entry
            in context.ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.MarkAsDeleted(deletedBy);
            }
        }
    }

    private string ResolveDeletedBy()
    {
        if (_userContext is { IsAuthenticated: true, UserId: { Length: > 0 } userId })
        {
            return userId;
        }

        return SystemUser;
    }
}
