namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

/// <summary>
/// <see cref="IHostedService"/> que aplica migrations EF Core do <typeparamref name="TContext"/>
/// no <c>StartAsync</c> do host. Encapsular a chamada de
/// <see cref="MigrationServiceCollectionExtensions.ApplyMigrationsAsync{TContext}"/> num hosted
/// service permite que test factories filtrem o registro (mesmo padrão usado para o
/// <c>WolverineRuntime</c> em <c>ApiFactoryBase</c>) — testes que sobem o pipeline HTTP sem
/// um Postgres real não tentam aplicar migrations contra <c>localhost:5432</c>.
/// </summary>
internal sealed class MigrationHostedService<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly IServiceProvider _services;

    public MigrationHostedService(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        _services.ApplyMigrationsAsync<TContext>(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
