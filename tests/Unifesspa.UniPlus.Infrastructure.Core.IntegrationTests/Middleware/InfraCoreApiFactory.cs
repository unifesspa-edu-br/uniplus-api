namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Middleware;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

// Fixture canônica para smoke tests do pipeline de middleware shared
// (issue #117 / #116). Hospeda o Program.cs do Selecao.API porque os
// 3 módulos (Selecao/Ingresso/Portal) cabeiam o middleware do
// Infrastructure.Core identicamente — cobrir via Selecao prova o
// pattern para o caso atual.
//
// Risco residual aceito: drift de wiring entre Selecao/Ingresso/Portal
// não é coberto por este smoke (não há arch test enforçando paridade
// hoje; CodeQL não prova ordem de middleware). Decisão CA-01 revisitada
// na issue #117 — quando o custo do drift aparecer, promover para
// matriz de 3 fixtures (uma por API) ou criar arch test que cabreie a
// invariante.
//
// Herda toda a infraestrutura do ApiFactoryBase:
//   - Wolverine + MigrationHostedService removidos (sem PG/Kafka reais)
//   - OpenTelemetry exporter silenciado (env var no static ctor)
//   - Health checks de infra externa filtrados
//   - TestAuthHandler plugado (scheme "Test")
//   - UseEnvironment("Development") — reusa appsettings.Development.json
//
// `GetConfigurationOverrides` fornece valores fake para chaves que o
// Program.cs lê em boot: connection string e Auth metadata. Sem eles,
// o `WebApplication.CreateBuilder` lançaria InvalidOperationException
// antes do primeiro request.
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x IClassFixture<T> requires the fixture type to be public.")]
public sealed class InfraCoreApiFactory : ApiFactoryBase<Program>
{
    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        // Connection string fake — Wolverine + MigrationHostedService já foram
        // removidos pela ApiFactoryBase, mas a validação inicial em
        // `AddSelecaoInfrastructure` ainda lê o valor (rejeita whitespace).
        new("ConnectionStrings:SelecaoDb", "Host=localhost;Port=5432;Database=uniplus_smoke;Username=u;Password=u"),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];
}
