namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Kernel.Domain.Entities;
using Kernel.Domain.Interfaces;

// Auditoria automática de criação/modificação (issue #390 + ADR-0033):
//   - Para toda EntityBase: preenche CreatedAt em Added e UpdatedAt em
//     Modified (comportamento original preservado).
//   - Para entidades que implementam IAuditableEntity (opt-in): adicionalmente
//     preenche CreatedBy/UpdatedBy a partir do IUserContext autenticado;
//     fallback "system" em fluxos sem principal (jobs, migrations).
//
// Registrado como Scoped na DI dos módulos (selecao/ingresso/portal) para
// acompanhar o ciclo de vida scoped do IUserContext (HttpUserContext) e do
// DbContext — Singleton aqui causaria captive dependency e o UserId
// congelaria no primeiro request servido pelo processo. Espelha o padrão
// do SoftDeleteInterceptor pós-#127.
public sealed class AuditableInterceptor : SaveChangesInterceptor
{
    private const string SystemUser = "system";

    private readonly IUserContext? _userContext;

    // Construtor sem args preservado para uso em testes que não exigem
    // IUserContext (cenário equivalente ao fallback "system").
    public AuditableInterceptor(IUserContext? userContext = null)
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
            ApplyAuditFields(eventData.Context);
        }

        return base.SavingChangesAsync(eventData!, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData?.Context is not null)
        {
            ApplyAuditFields(eventData.Context);
        }

        return base.SavingChanges(eventData!, result);
    }

    private void ApplyAuditFields(DbContext context)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string userBy = ResolveUserBy();

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<EntityBase> entry
            in context.ChangeTracker.Entries<EntityBase>())
        {
            bool isAuditable = entry.Entity is IAuditableEntity;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(EntityBase.CreatedAt)).CurrentValue = now;
                    if (isAuditable)
                    {
                        entry.Property(nameof(IAuditableEntity.CreatedBy)).CurrentValue = userBy;
                    }
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(EntityBase.UpdatedAt)).CurrentValue = now;
                    if (isAuditable)
                    {
                        entry.Property(nameof(IAuditableEntity.UpdatedBy)).CurrentValue = userBy;
                    }
                    break;
            }
        }
    }

    private string ResolveUserBy()
    {
        if (_userContext is { IsAuthenticated: true, UserId: { Length: > 0 } userId })
        {
            return userId;
        }

        return SystemUser;
    }
}
