namespace Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// que sobe o composition root do monólito modular apontando as 5
/// connection strings para o Postgres efêmero da fixture — banco único
/// <c>uniplus</c> com schema-por-módulo.
/// </summary>
/// <remarks>
/// <para><strong>Entry point via <see cref="HostAssemblyMarker"/>:</strong> o Host
/// co-hospeda os 4 módulos, cada um com um <c>public partial class Program</c> no
/// namespace global — referenciar <c>Program</c> diretamente seria ambíguo
/// (CS0433). O marcador público do assembly do Host direciona o
/// <c>WebApplicationFactory</c> ao entry point correto sem ambiguidade.</para>
///
/// <para><strong>Wolverine habilitado:</strong> ao contrário da maioria das
/// suítes HTTP-only, esta sobe o pipeline produtivo completo — as migrations on
/// startup dos 4 módulos criam os schemas e o Wolverine provisiona o outbox
/// (schema <c>wolverine</c>). Isso valida o boot em runtime além da leitura
/// in-process. A fixture provisiona o Postgres, então o runtime Wolverine
/// não pode ser removido.</para>
///
/// <para><strong>Cache:</strong> <see cref="ICacheService"/> é substituído por
/// <see cref="FakeInMemoryCacheService"/> — o que se prova é o caminho de leitura
/// in-process até o banco, não a camada de cache Redis (ortogonal). Evita um
/// container Redis e mantém o teste determinístico.</para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative used as collection fixture state.")]
public sealed class MonolitoHostApiFactory : ApiFactoryBase<HostAssemblyMarker>
{
    private readonly string _connectionString;

    public MonolitoHostApiFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    // A fixture provisiona Postgres efêmero — as migrations on startup e o
    // runtime Wolverine devem rodar para criar os 4 schemas + outbox.
    protected override bool DisableWolverineRuntimeForTests => false;

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        // As 5 connection strings apontam para o MESMO banco `uniplus`; cada
        // DbContext usa seu schema (HasDefaultSchema). UniPlusDb é o outbox
        // Wolverine (schema `wolverine`) + health checks.
        new("ConnectionStrings:UniPlusDb", _connectionString),
        new("ConnectionStrings:ConfiguracaoDb", _connectionString),
        new("ConnectionStrings:OrganizacaoDb", _connectionString),
        new("ConnectionStrings:SelecaoDb", _connectionString),
        new("ConnectionStrings:IngressoDb", _connectionString),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Substitui o RedisCacheService produtivo por um fake in-memory que
            // sempre dá cache miss — o reader cai à fonte (DB in-process) sem
            // exigir um container Redis. Scoped: espelha o lifetime produtivo
            // (depende de DbContext scoped no caminho real).
            services.RemoveAll<ICacheService>();
            services.AddScoped<ICacheService, FakeInMemoryCacheService>();
        });
    }
}
