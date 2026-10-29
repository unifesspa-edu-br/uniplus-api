namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Diagnostics.CodeAnalysis;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Monólito com Postgres e um Mailpit efêmeros: o relay de e-mail do host aponta para o contêiner de
/// captura, e a API HTTP dele é o que os testes consultam para ver a mensagem entregue.
/// </summary>
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por EncerrarDependenciasAsync — xUnit invoca deterministicamente.")]
public sealed class MonolitoComMailpitFixture : MonolitoPostgresFixtureBase<MonolitoComMailpitApiFactory>
{
    private const ushort PortaSmtp = 1025;
    private const ushort PortaHttp = 8025;

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.27")
        .WithPortBinding(PortaSmtp, true)
        .WithPortBinding(PortaHttp, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(PortaHttp).ForPath("/readyz")))
        .Build();

    public Uri ApiDoMailpit => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(PortaHttp)}");

    protected override Task IniciarDependenciasAsync() => _mailpit.StartAsync();

    protected override async Task EncerrarDependenciasAsync()
    {
        try
        {
            await _mailpit.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            await _mailpit.DisposeAsync().ConfigureAwait(false);
        }
    }

    protected override MonolitoComMailpitApiFactory CreateFactory(string connectionString) =>
        new(connectionString, _mailpit.Hostname, _mailpit.GetMappedPublicPort(PortaSmtp));
}

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative usado como estado de fixture.")]
public sealed class MonolitoComMailpitApiFactory : MonolitoApiFactory
{
    private readonly string _hostSmtp;
    private readonly int _portaSmtp;
    private int _falhas;

    public MonolitoComMailpitApiFactory(string connectionString, string hostSmtp, int portaSmtp)
        : base(connectionString, wolverineEnabled: true)
    {
        _hostSmtp = hostSmtp;
        _portaSmtp = portaSmtp;
    }

    /// <summary>Quantas das próximas tentativas de entrega o relay deve recusar, como se estivesse fora do ar.</summary>
    public int FalhasPendentes
    {
        set => Volatile.Write(ref _falhas, value);
    }

    protected override IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() =>
    [
        new("Email:Smtp:Host", _hostSmtp),
        new("Email:Smtp:Porta", _portaSmtp.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("Email:Smtp:Seguranca", "Nenhuma"),
        new("Email:Smtp:Remetente", "nao-responda@uniplus.local"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITransporteDeEmail>();
            services.AddSingleton<ITransporteDeEmail>(sp => new TransporteQueFalhaAntes(
                new SmtpTransporteDeEmail(
                    sp.GetRequiredService<IOptions<EmailSmtpOptions>>(),
                    sp.GetRequiredService<ILogger<SmtpTransporteDeEmail>>()),
                this));
        });
    }

    // Limite inferior em zero: decremento cego deixaria o contador negativo e mascararia um próximo ajuste.
    private static bool ConsumirFalha(ref int falhas)
    {
        int atual;
        do
        {
            atual = Volatile.Read(ref falhas);
            if (atual <= 0)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref falhas, atual - 1, atual) != atual);

        return true;
    }

    private sealed class TransporteQueFalhaAntes(ITransporteDeEmail real, MonolitoComMailpitApiFactory fabrica) : ITransporteDeEmail
    {
        public Task EnviarAsync(EnviarEmail mensagem, CancellationToken cancellationToken)
        {
            if (ConsumirFalha(ref fabrica._falhas))
            {
                throw new InvalidOperationException("Relay indisponível (simulado).");
            }

            return real.EnviarAsync(mensagem, cancellationToken);
        }
    }
}
