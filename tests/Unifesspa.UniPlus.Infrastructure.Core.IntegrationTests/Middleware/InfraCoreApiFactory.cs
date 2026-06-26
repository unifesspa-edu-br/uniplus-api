namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Middleware;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

// Fixture canônica para smoke tests do pipeline de middleware shared
// (issue #117 / #116). Sobe a API UniPlus (composition root do monólito
// modular) via MonolitoApiFactory — o middleware do Infrastructure.Core é
// cabeado pelo host, então o smoke exercita o wiring real de produção, sem
// depender de um Program.cs por módulo (que deixou de existir quando os
// módulos viraram class libraries).
//
// Herda toda a infraestrutura HTTP-only do MonolitoApiFactory/ApiFactoryBase:
//   - Wolverine + MigrationHostedService removidos (wolverineEnabled: false,
//     sem PG/Kafka reais)
//   - OpenTelemetry exporter silenciado (env var no static ctor da base)
//   - Health checks de infra externa filtrados
//   - TestAuthHandler plugado (scheme "Test")
//   - ICacheService trocado por FakeInMemoryCacheService (sem Redis)
//   - UseEnvironment("Development") — reusa appsettings.Development.json do host
//   - 5 connection strings fake + Auth metadata fornecidos pela base, que o host
//     lê em boot (rejeita whitespace) sem nunca abrir conexão real.
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x IClassFixture<T> requires the fixture type to be public.")]
public sealed class InfraCoreApiFactory : MonolitoApiFactory
{
    public InfraCoreApiFactory()
        : base(
            "Host=localhost;Port=5432;Database=uniplus;Username=uniplus;Password=uniplus_dev",
            wolverineEnabled: false)
    {
    }
}
