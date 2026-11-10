namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// A etapa que o módulo pede para rodar logo depois das migrations do <typeparamref name="TContext"/>.
/// </summary>
internal sealed record EtapaPosteriorDaMigration<TContext>(Func<IServiceProvider, CancellationToken, Task> Executar)
    where TContext : DbContext;
