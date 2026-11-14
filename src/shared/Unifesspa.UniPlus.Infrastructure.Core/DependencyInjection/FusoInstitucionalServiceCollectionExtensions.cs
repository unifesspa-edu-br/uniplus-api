namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Instantes;

public static class FusoInstitucionalServiceCollectionExtensions
{
    /// <summary>
    /// Registra o resolvedor do fuso institucional. Singleton: a zona é constante e o resultado da busca na base de
    /// fusos do sistema não muda durante a execução — inclusive quando a base falta, que é o caso que o erro nomeado
    /// cobre.
    /// </summary>
    public static IServiceCollection AddFusoInstitucional(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IResolvedorFusoInstitucional, ResolvedorFusoInstitucional>();

        return services;
    }
}
