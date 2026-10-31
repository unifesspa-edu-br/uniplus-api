namespace Unifesspa.UniPlus.Autorizacao.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;

/// <summary>
/// Registra a camada Application do módulo Autorização. O CQRS roda sobre Wolverine, então
/// só os validators FluentValidation, consumidos pelo middleware do Wolverine, são registrados.
/// </summary>
public static class AutorizacaoApplicationServiceRegistration
{
    public static IServiceCollection AddAutorizacaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssembly(typeof(AutorizacaoApplicationServiceRegistration).Assembly);

        // As origens "instalação" e "pedido" acrescentam os eventos delas à trilha da concessão que gerou.
        services.AddScoped<IEventosDaOrigem, EventosDaInstalacao>();
        services.AddScoped<IEventosDaOrigem, EventosDoPedido>();

        services.AddScoped<AvisosDoPedido>();

        return services;
    }
}
