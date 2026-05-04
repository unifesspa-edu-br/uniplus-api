namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Messaging;

public static class WolverineMessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registra os wrappers <see cref="ICommandBus"/> e <see cref="IQueryBus"/>
    /// que delegam para o <c>Wolverine.IMessageBus</c>. Pré-requisito:
    /// <c>UseWolverine(...)</c> configurado no host (responsável por registrar
    /// <c>IMessageBus</c> em DI). Ver ADR-0003.
    /// </summary>
    public static IServiceCollection AddWolverineMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Wolverine.IMessageBus é registrado como scoped pelo UseWolverine; os wrappers
        // herdam o mesmo lifetime para preservar contexto de transação/correlation por request.
        services.AddScoped<ICommandBus, WolverineCommandBus>();
        services.AddScoped<IQueryBus, WolverineQueryBus>();
        return services;
    }
}
