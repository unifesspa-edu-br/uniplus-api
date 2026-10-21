namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosDaInscricao;

using System.Diagnostics.CodeAnalysis;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

/// <summary>
/// Sobe o clamd (imagem oficial do ClamAV, a mesma do docker-compose) via Testcontainers. A imagem já
/// traz as assinaturas, então o clamd responde em segundos.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + IClassFixture<T> exigem tipo público.")]
public sealed class ClamAvContainerFixture : IAsyncLifetime
{
    public const string Image = "clamav/clamav:1.4@sha256:3bba6dd5ac6663d55b80f86a3126e146612d93b593cc2617188d13f637d91260";

    private const ushort ClamdPort = 3310;

    private readonly IContainer _container = new ContainerBuilder(Image)
        .WithPortBinding(ClamdPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("socket found, clamd started"))
        .Build();

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(ClamdPort);

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);
}
