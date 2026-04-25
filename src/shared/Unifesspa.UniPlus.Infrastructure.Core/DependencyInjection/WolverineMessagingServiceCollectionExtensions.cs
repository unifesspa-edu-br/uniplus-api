namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public static class WolverineMessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registra os wrappers <see cref="ICommandBus"/> e <see cref="IDomainEventDispatcher"/>
    /// que delegam para o <c>Wolverine.IMessageBus</c>. Pré-requisito: <c>UseWolverine(...)</c>
    /// configurado no host (responsável por registrar <c>IMessageBus</c> em DI). Ver
    /// <a href="../../../../../docs/adrs/ADR-022-backbone-cqrs-wolverine.md">ADR-022</a>.
    /// </summary>
    public static IServiceCollection AddWolverineMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Wolverine.IMessageBus é registrado como scoped pelo UseWolverine; os wrappers
        // herdam o mesmo lifetime para preservar contexto de transação/correlation por request.
        services.AddScoped<ICommandBus, WolverineCommandBus>();
        services.AddScoped<IDomainEventDispatcher, WolverineDomainEventDispatcher>();
        return services;
    }
}
