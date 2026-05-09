namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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

    /// <summary>Cria provider com stub HTTP que responde 200 OK — útil para
    /// verificar que o provider continua funcional (não disposed) executando
    /// o caminho real que toca o SemaphoreSlim interno.</summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Factory: ownership do provider passa ao caller (using); HttpClient é owned pelo provider.")]
    private static OAuthBearerAuthenticationHeaderValueProvider CreateProviderWithStub(StubHttpMessageHandler handler)
    {
        string okResponse = JsonSerializer.Serialize(new
        {
            access_token = "jwt-test",
            expires_in = 300,
            token_type = "Bearer",
        });
        handler.EnqueueResponse(HttpStatusCode.OK, okResponse);

        return new OAuthBearerAuthenticationHeaderValueProvider(
            httpClient: new HttpClient(handler),
            ownsHttpClient: true,
            settings: new OAuthBearerSettings
            {
                ClientId = "test",
                ClientSecret = "test",
                TokenEndpoint = "https://kc.test/token",
            },
            logger: NullLogger<OAuthBearerAuthenticationHeaderValueProvider>.Instance);
    }

    [Fact(DisplayName = "StartAsync é no-op — retorna Task concluída e provider continua funcional")]
    public async Task StartAsync_DeveSerNoOp()
    {
        using StubHttpMessageHandler handler = new();
        using OAuthBearerAuthenticationHeaderValueProvider provider = CreateProviderWithStub(handler);
        OAuthBearerAuthProviderDisposeHostedService sut = new(provider);

        Task task = sut.StartAsync(CancellationToken.None);

        // Asserção de comportamento (não de identidade) — Task.CompletedTask
        // não garante que cada acesso retorna a mesma instância em todos os
        // runtimes, então BeSameAs flake-aria. Validar o que importa: já
        // completou síncrono e com sucesso.
        task.IsCompletedSuccessfully.Should().BeTrue(
            because: "StartAsync é no-op síncrono — deve retornar Task já concluída.");

        // Operação real que depende do estado não-disposto: GetAuthenticationHeader
        // entra no caminho de refresh (cache cold), bate no SemaphoreSlim e dispara
        // o request HTTP. Se StartAsync regredir e chamar Dispose(), esta linha
        // lança ObjectDisposedException — falha observável (vs GetType() que
        // sempre passa, mesmo após dispose).
        Action realCall = () => _ = provider.GetAuthenticationHeader();
        realCall.Should().NotThrow<ObjectDisposedException>(
            because: "StartAsync deve ser no-op; provider precisa continuar funcional após StartAsync.");

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
