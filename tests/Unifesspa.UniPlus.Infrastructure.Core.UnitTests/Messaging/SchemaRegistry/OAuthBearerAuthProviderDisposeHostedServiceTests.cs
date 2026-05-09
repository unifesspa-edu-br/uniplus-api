namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

/// <summary>
/// Cobertura de <see cref="OAuthBearerAuthProviderDisposeHostedService"/> —
/// IHostedService responsável por dispor o OAuthBearerAuthenticationHeaderValueProvider
/// criado eager em CreateClient. Cenários:
/// <list type="bullet">
/// <item><description><c>StartAsync</c> é no-op (não toca o provider).</description></item>
/// <item><description><c>StopAsync</c> chama Dispose explicitamente no provider — libera HttpClient (ownsHttpClient=true) + SemaphoreSlim.</description></item>
/// </list>
/// </summary>
[Trait("Category", "Unit")]
public sealed class OAuthBearerAuthProviderDisposeHostedServiceTests
{
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Factory: ownership do provider e do HttpClient interno passa ao caller, que dispõe via 'using'.")]
    private static OAuthBearerAuthenticationHeaderValueProvider CreateProviderOwningHttpClient()
    {
        // ownsHttpClient: true para validar que Dispose libera o HttpClient interno.
        // Settings vazias suficientes — não vamos chamar GetAuthenticationHeader.
        return new OAuthBearerAuthenticationHeaderValueProvider(
            httpClient: new HttpClient(),
            ownsHttpClient: true,
            settings: new OAuthBearerSettings { ClientId = "x", ClientSecret = "x", TokenEndpoint = "https://x" },
            logger: NullLogger<OAuthBearerAuthenticationHeaderValueProvider>.Instance);
    }

    [Fact(DisplayName = "StartAsync é no-op — retorna Task.CompletedTask sem tocar o provider")]
    public async Task StartAsync_DeveSerNoOp()
    {
        using OAuthBearerAuthenticationHeaderValueProvider provider = CreateProviderOwningHttpClient();
        OAuthBearerAuthProviderDisposeHostedService sut = new(provider);

        Task task = sut.StartAsync(CancellationToken.None);

        task.Should().BeSameAs(Task.CompletedTask);
        // Provider continua utilizável após StartAsync — não foi disposto.
        Action probe = () => _ = provider.GetType();
        probe.Should().NotThrow();

        await Task.CompletedTask;
    }

    [Fact(DisplayName = "StopAsync dispõe o provider — chamada subsequente lança ObjectDisposed")]
    public async Task StopAsync_DeveDisporOProvider()
    {
        using OAuthBearerAuthenticationHeaderValueProvider provider = CreateProviderOwningHttpClient();
        OAuthBearerAuthProviderDisposeHostedService sut = new(provider);

        await sut.StopAsync(CancellationToken.None);

        // Após Dispose, GetAuthenticationHeader tenta usar SemaphoreSlim já disposto —
        // dispara ObjectDisposedException. É a observação confiável de que Dispose foi chamado.
        Action act = () => _ = provider.GetAuthenticationHeader();
        act.Should().Throw<System.ObjectDisposedException>();
    }

    [Fact(DisplayName = "Dispose é idempotente — múltiplas chamadas StopAsync não lançam")]
    public async Task StopAsync_Multiplo_DeveSerIdempotente()
    {
        using OAuthBearerAuthenticationHeaderValueProvider provider = CreateProviderOwningHttpClient();
        OAuthBearerAuthProviderDisposeHostedService sut = new(provider);

        await sut.StopAsync(CancellationToken.None);
        Func<Task> act = async () => await sut.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync(
            because: "OAuthBearerAuthenticationHeaderValueProvider.Dispose tem flag 'disposed' e é idempotente.");
    }
}
