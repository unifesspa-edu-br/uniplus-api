namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;

using Kernel.Domain.Entities;
using Kernel.Domain.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

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
    private readonly TimeProvider _timeProvider;

    // TimeProvider é obrigatório (sem fallback TimeProvider.System): o relógio
    // é sempre injetado pela DI (Singleton). IUserContext permanece opcional —
    // o fallback "system" é regra legítima para fluxos sem principal (jobs,
    // migrations), não um backdoor de não-determinismo.
    public AuditableInterceptor(TimeProvider timeProvider, IUserContext? userContext = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
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
        DateTimeOffset now = _timeProvider.GetUtcNow();
        string userBy = ResolveUserBy();

        MarcarDonosDeOwnedAlterado(context);

        foreach (EntityEntry<EntityBase> entry in context.ChangeTracker.Entries<EntityBase>())
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

    /// <summary>
    /// Editar só uma coleção owned (<c>OwnsMany</c>) muda apenas as linhas filhas, e o dono
    /// continuaria <see cref="EntityState.Unchanged"/>: a trilha não registraria quem mudou nem
    /// quando. O dono de cada entrada owned alterada passa a <see cref="EntityState.Modified"/>.
    /// </summary>
    /// <remarks>
    /// O dono é achado pela chave da posse, e não pela navegação: o item removido da coleção
    /// some dela, e só a chave estrangeira ainda aponta o dono. Marca-se só
    /// <see cref="EntityBase.UpdatedAt"/>, que o carimbo regrava em seguida: marcar a entrada
    /// inteira regravaria as demais colunas e desfaria, por exemplo, uma remoção lógica
    /// concorrente.
    /// </remarks>
    private static void MarcarDonosDeOwnedAlterado(DbContext context)
    {
        List<EntityEntry> alteradas = [.. context.ChangeTracker.Entries()
            .Where(static e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && e.Metadata.IsOwned())];
        if (alteradas.Count == 0)
        {
            return;
        }

        // Indexadas uma vez, por tipo (inclusive os tipos base) e chave primária: o interceptor
        // roda em todo SaveChanges, e procurar o dono varrendo as entradas a cada item owned
        // custaria o quadrado do agregado.
        Dictionary<(IReadOnlyEntityType Tipo, string Chave), EntityEntry> porChave = [];
        foreach (EntityEntry entrada in context.ChangeTracker.Entries())
        {
            if (entrada.Metadata.FindPrimaryKey() is not { } chavePrimaria)
            {
                continue;
            }

            string chave = Chave(chavePrimaria.Properties.Select(p => entrada.Property(p.Name).CurrentValue));
            foreach (IReadOnlyEntityType tipo in entrada.Metadata.GetAllBaseTypesInclusive())
            {
                porChave.TryAdd((tipo, chave), entrada);
            }
        }

        foreach (EntityEntry owned in alteradas)
        {
            EntityEntry? dono = owned;
            while (dono?.Metadata.FindOwnership() is { } posse)
            {
                string chaveDoDono = Chave(posse.Properties.Select(p => dono.Property(p.Name).CurrentValue));
                dono = porChave.GetValueOrDefault((posse.PrincipalEntityType, chaveDoDono));
            }

            if (dono is { State: EntityState.Unchanged, Entity: EntityBase })
            {
                dono.Property(nameof(EntityBase.UpdatedAt)).IsModified = true;
            }
        }
    }

    private static string Chave(IEnumerable<object?> valores) =>
        string.Join('\u001f', valores.Select(static v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)));

    private string ResolveUserBy()
    {
        if (_userContext is { IsAuthenticated: true, UserId: { Length: > 0 } userId })
        {
            return userId;
        }

        return SystemUser;
    }
}
