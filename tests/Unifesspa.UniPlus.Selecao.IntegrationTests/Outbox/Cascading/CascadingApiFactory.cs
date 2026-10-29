namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

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
/// <see cref="DomainEventCollector"/> que o handler subscritor consome, e o acervo público em
/// memória: a divulgação publica o documento do edital, e esta fábrica não sobe armazenamento.
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
            services.AdicionarAdministradorDeProcessosDeTeste();
            services.RemoveAll<IRegistroOperacionalRestrito>();
            services.AddSingleton<RegistroDeDecisoesDeTeste>();
            services.AddSingleton<IRegistroOperacionalRestrito>(sp => sp.GetRequiredService<RegistroDeDecisoesDeTeste>());
            services.RemoveAll<IAcervoPublico>();
            services.AddSingleton<IAcervoPublico, AcervoPublicoEmMemoria>();
        });
    }
}
