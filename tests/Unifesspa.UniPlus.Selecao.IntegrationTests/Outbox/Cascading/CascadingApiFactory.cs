namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Factory que sobe a API UniPlus (composition root) com Wolverine + migrations
/// ativos, apontando para o Postgres efêmero da fixture. Valida a infra produtiva
/// de outbox (PG queue, durable envelopes, cascading drainage) através do host —
/// Selecao é uma library exercitada por ele.
/// </summary>
/// <remarks>
/// O re-registro manual do <c>SelecaoDbContext</c> que existia aqui tornou-se
/// redundante: o host registra o DbContext via
/// <c>AddSelecaoInfrastructure</c> com todos os interceptors
/// (SoftDelete + Auditable + ObrigatoriedadeLegalHistorico) e snake_case por
/// convenção (<c>UseUniPlusNpgsqlConventions</c>). Resta apenas o
/// <see cref="DomainEventCollector"/> que o handler subscritor consome.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative used as collection fixture state.")]
public sealed class CascadingApiFactory : MonolitoApiFactory
{
    public CascadingApiFactory(string connectionString)
        : base(connectionString, wolverineEnabled: true)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<DomainEventCollector>();
        });
    }
}
