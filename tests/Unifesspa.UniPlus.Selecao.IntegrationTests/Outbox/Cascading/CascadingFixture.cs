namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Fixture da suíte de outbox cascading: provisiona Postgres efêmero e sobe a API
/// UniPlus com Wolverine + migrations ativos, via <see cref="CascadingApiFactory"/>
/// (que adiciona o <see cref="DomainEventCollector"/> consumido pelos handlers de
/// teste). Reaproveita o ciclo de vida do container e o gerenciamento das 5 env vars
/// de connection string da <see cref="MonolitoPostgresFixtureBase{TFactory}"/> —
/// inclusive o desligamento do transporte Kafka (<c>Kafka__BootstrapServers</c> em
/// whitespace), já que esta suíte valida apenas a PG queue.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the fixture type to be public.")]
public sealed class CascadingFixture : MonolitoPostgresFixtureBase<CascadingApiFactory>
{
    protected override CascadingApiFactory CreateFactory(string connectionString) =>
        new(connectionString);
}
