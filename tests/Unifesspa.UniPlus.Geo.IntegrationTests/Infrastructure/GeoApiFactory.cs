namespace Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Factory que sobe a API do Geo contra o Postgres+PostGIS efêmero provisionado
/// por <see cref="GeoPostgisFixture"/>. A connection string chega via env var
/// <c>ConnectionStrings__GeoDb</c> setada pela fixture (lida a tempo pelo
/// <c>WebApplicationBuilder</c>, ao contrário de <c>ConfigureAppConfiguration</c>).
/// </summary>
/// <remarks>
/// <para><c>DisableWolverineRuntimeForTests = false</c>: o host roda a migration
/// (cria a extensão postgis + tabela-sonda) e o WolverineRuntime contra o banco
/// efêmero — exatamente o caminho produtivo.</para>
/// <para>Os health checks de infra não provisionada (redis/minio/kafka) e o
/// <c>oidc-discovery</c> (sem IdP em teste) são removidos para que
/// <c>/health/ready</c> avalie apenas o <c>postgres</c> — provando o readiness
/// contra PG+PostGIS real (CA-02).</para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit fixtures exigem tipo público.")]
public sealed class GeoApiFactory : ApiFactoryBase<Program>
{
    protected override bool DisableWolverineRuntimeForTests => false;

    protected override ISet<string> InfraHealthCheckNamesToRemoveForTests { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "redis", "minio", "kafka", "oidc-discovery" };

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
        // Worker do ETL desligado: o disparo (POST) cria o registro EmAndamento e
        // retorna 202 sem que a carga real rode — torna 202/409 determinísticos (#674).
        new("Geo:Etl:WorkerHabilitado", "false"),
        // Redis vazio: o lookup de CEP (#676) degrada para o banco de forma rápida e
        // determinística (sem o connect lento ao localhost:6379 do appsettings.Development).
        // O comportamento de cache-aside é coberto pelos testes de unidade do CepResolver.
        new("Redis:ConnectionString", string.Empty),
    ];
}
