namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dono do <see cref="ISchemaRegistryClient"/> criado no bootstrap — antes de o <see cref="IServiceProvider"/>
/// existir, porque o callback do <c>UseWolverine</c> precisa do cliente para montar o serializador — e do provider
/// OAuth que o cliente usa.
/// </summary>
/// <remarks>
/// O <c>CachedSchemaRegistryClient</c> não dispõe o provider de autenticação no próprio <c>Dispose</c>, e o container
/// só dispõe o que ele mesmo instancia: <c>AddSingleton(instância)</c> não transfere a posse. Por isso o dono é
/// registrado como <see cref="IHostedService"/> por factory: o container o instancia na partida do host, o captura e o
/// dispõe uma única vez no encerramento, liberando o cliente, o provider, o <c>HttpClient</c> e o semáforo de
/// renovação. <see cref="StartAsync"/> e <see cref="StopAsync"/> não fazem nada.
/// </remarks>
internal sealed class SchemaRegistryDoBootstrap : IHostedService, IDisposable
{
    private readonly OAuthBearerAuthenticationHeaderValueProvider? _autenticacao;

    public SchemaRegistryDoBootstrap(SchemaRegistrySettings settings, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        SchemaRegistryConfig configuracao = SchemaRegistryServiceCollectionExtensions.ConfiguracaoDoCliente(settings);
        _autenticacao = SchemaRegistryServiceCollectionExtensions.UsaOAuthBearer(settings)
            ? AutenticacaoOAuth(settings, loggerFactory)
            : null;
        Cliente = _autenticacao is null
            ? new CachedSchemaRegistryClient(configuracao)
            : new CachedSchemaRegistryClient(configuracao, _autenticacao);
    }

    public ISchemaRegistryClient Cliente { get; }

    /// <summary>O provider OAuth do cliente; nulo quando a autenticação não é OAuth bearer.</summary>
    public IAuthenticationHeaderValueProvider? Autenticacao => _autenticacao;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose()
    {
        Cliente.Dispose();
        _autenticacao?.Dispose();
    }

    // Composition root genuíno (o callback do Wolverine roda antes de builder.Build(), sem IServiceProvider): o
    // HttpClient é próprio do provider, que o dispõe.
    private static OAuthBearerAuthenticationHeaderValueProvider AutenticacaoOAuth(
        SchemaRegistrySettings settings, ILoggerFactory loggerFactory) =>
        new(
            new HttpClient(),
            ownsHttpClient: true,
            settings.OAuth,
            loggerFactory.CreateLogger<OAuthBearerAuthenticationHeaderValueProvider>(),
            TimeProvider.System);
}
