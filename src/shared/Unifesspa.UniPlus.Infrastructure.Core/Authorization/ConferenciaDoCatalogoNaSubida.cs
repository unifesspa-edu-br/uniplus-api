namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Authorization.Catalogo;

/// <summary>
/// Confere o catálogo reunido de todos os módulos quando a aplicação sobe. Resolver o
/// <see cref="CatalogoDaAplicacao"/> executa as conferências do conjunto; se alguma
/// falha, a subida é abortada e a mensagem nomeia o membro responsável (ADR-0151).
/// </summary>
internal sealed class ConferenciaDoCatalogoNaSubida : IHostedService
{
    private readonly IServiceProvider _services;

    public ConferenciaDoCatalogoNaSubida(IServiceProvider services)
    {
        _services = services;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = _services.GetRequiredService<CatalogoDaAplicacao>();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
