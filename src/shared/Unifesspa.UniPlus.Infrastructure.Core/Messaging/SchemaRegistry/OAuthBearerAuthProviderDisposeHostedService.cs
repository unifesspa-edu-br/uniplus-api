namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

/// <summary>
/// <see cref="IHostedService"/> de "tear-down ownership" — dispõe explicitamente o
/// <see cref="OAuthBearerAuthenticationHeaderValueProvider"/> standalone (criado em
/// <see cref="SchemaRegistryServiceCollectionExtensions.CreateClient"/>) no shutdown
/// do <see cref="IHost"/>.
/// </summary>
/// <remarks>
/// <para>
/// Por que esta classe existe (#360, eco do Codex P2 no PR #361):
/// <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton{TService}(Microsoft.Extensions.DependencyInjection.IServiceCollection, TService)"/>
/// com instância pré-criada (criada fora do container) <b>não transfere ownership</b>
/// ao IServiceProvider — o container só dispõe o que ele próprio instancia (via tipo
/// ou factory delegate). Logo, registrar
/// <see cref="OAuthBearerAuthenticationHeaderValueProvider"/> como instância singleton
/// não basta para garantir dispose.
/// </para>
/// <para>
/// O caminho idiomático Microsoft para esse cenário (instância pré-criada por motivos
/// arquiteturais — neste caso, o cliente Wolverine precisa do auth provider eager no
/// closure de <c>UseWolverine</c>, que roda antes de <c>builder.Build()</c>) é
/// registrar um <see cref="IHostedService"/> com o auth provider injetado: container
/// instancia o hosted service (e o dispõe no shutdown), o hosted service chama
/// <c>Dispose()</c> explicitamente em <see cref="StopAsync"/>.
/// </para>
/// <para>
/// Lifecycle: <see cref="StartAsync"/> é no-op. <see cref="StopAsync"/> chama
/// <see cref="OAuthBearerAuthenticationHeaderValueProvider.Dispose"/> que libera o
/// <c>HttpClient</c> standalone (com <c>ownsHttpClient: true</c>) e o
/// <c>SemaphoreSlim</c> de refresh.
/// </para>
/// </remarks>
internal sealed class OAuthBearerAuthProviderDisposeHostedService : IHostedService
{
    private readonly OAuthBearerAuthenticationHeaderValueProvider provider;

    public OAuthBearerAuthProviderDisposeHostedService(OAuthBearerAuthenticationHeaderValueProvider provider)
    {
        this.provider = provider;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        provider.Dispose();
        return Task.CompletedTask;
    }
}
