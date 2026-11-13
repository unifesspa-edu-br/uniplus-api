namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
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
    private readonly IContainer _mailpit = MailpitDeTeste.Criar();

    public Uri ApiDoMailpit => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(MailpitDeTeste.PortaHttp)}");

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
        new(connectionString, _mailpit.Hostname, _mailpit.GetMappedPublicPort(MailpitDeTeste.PortaSmtp));
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

    /// <summary>Falha no commit da transação do <c>ConfiguracaoDbContext</c> que alterou uma entidade, depois do outbox.</summary>
    public FalhaNoCommitDaSolicitacao FalhaNoCommit { get; } = new();

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
            services.ConfigureDbContext<ConfiguracaoDbContext>(opcoes => opcoes.AddInterceptors(FalhaNoCommit));
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

/// <summary>
/// Faz a transação que alterou uma entidade falhar no commit, uma vez, quando armada. O Wolverine grava o envelope do aviso
/// no outbox depois que o handler retorna e antes do commit, na mesma transação; por isso a falha é lançada no commit, e
/// antes de lançar ela conta, na mesma conexão e transação, os envelopes de e-mail já gravados.
/// </summary>
public sealed class FalhaNoCommitDaSolicitacao : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private int _armada;
    private int _disparos;
    private int _envelopesNaTransacao;
    private bool _alterouEntidade;

    /// <summary>Quantas vezes a falha foi disparada.</summary>
    public int Disparos => Volatile.Read(ref _disparos);

    /// <summary>Envelopes de e-mail visíveis na transação no instante da falha: a prova de que o aviso entrou nela.</summary>
    public int EnvelopesNaTransacao => Volatile.Read(ref _envelopesNaTransacao);

    /// <summary>Arma a falha para o próximo commit de uma transação que alterou uma entidade existente.</summary>
    public void Armar() => Volatile.Write(ref _armada, 1);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context!.ChangeTracker.Entries().Any(static e => e.State is Microsoft.EntityFrameworkCore.EntityState.Modified))
        {
            _alterouEntidade = true;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    async ValueTask<InterceptionResult> IDbTransactionInterceptor.TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (_alterouEntidade && Interlocked.CompareExchange(ref _armada, 0, 1) == 1)
        {
            Volatile.Write(ref _envelopesNaTransacao, await ContarEnvelopesDeEmailAsync(transaction, cancellationToken));
            Interlocked.Increment(ref _disparos);
            _alterouEntidade = false;

            throw new InvalidOperationException("Falha no commit (simulada).");
        }

        return result;
    }

    private static async Task<int> ContarEnvelopesDeEmailAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using DbCommand contagem = transaction.Connection!.CreateCommand();
        contagem.Transaction = transaction;
        contagem.CommandText = "SELECT count(*) FROM wolverine.wolverine_outgoing_envelopes WHERE message_type ILIKE '%email%'";
        return Convert.ToInt32(await contagem.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}
