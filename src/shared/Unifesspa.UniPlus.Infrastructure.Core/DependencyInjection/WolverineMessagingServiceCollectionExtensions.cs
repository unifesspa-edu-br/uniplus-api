namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public static class WolverineMessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registra o wrapper <see cref="ICommandBus"/> que delega para o
    /// <c>Wolverine.IMessageBus</c>. Pré-requisito: <c>UseWolverine(...)</c>
    /// configurado no host (responsável por registrar <c>IMessageBus</c> em DI).
    /// Ver ADR-022 (uniplus-docs/docs/adrs/).
    /// </summary>
    public static IServiceCollection AddWolverineMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Wolverine.IMessageBus é registrado como scoped pelo UseWolverine; o wrapper
        // herda o mesmo lifetime para preservar contexto de transação/correlation por request.
        services.AddScoped<ICommandBus, WolverineCommandBus>();
        return services;
    }
}
