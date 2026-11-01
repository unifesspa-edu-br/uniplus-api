namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// Constrói o registro de acesso quando a aplicação sobe, para que configuração inválida ou recusada pelo cliente
/// Kafka aborte a subida: a aplicação não entrega dado protegido sem registro. Não confere se o broker está
/// acessível nem se o tópico existe; isso aparece como falha de gravação (503) na primeira execução.
/// </summary>
internal sealed class ConferenciaDoRegistroDeAcessoNaSubida : IHostedService
{
    private readonly IServiceProvider _services;

    public ConferenciaDoRegistroDeAcessoNaSubida(IServiceProvider services)
    {
        _services = services;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = _services.GetRequiredService<IRegistroOperacionalRestrito>();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
