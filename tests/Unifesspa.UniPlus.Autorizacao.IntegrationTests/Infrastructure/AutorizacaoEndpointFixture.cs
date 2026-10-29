namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Postgres efêmero e API do monólito com Wolverine habilitado, para exercitar os endpoints do
/// módulo Autorização de ponta a ponta.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the fixture type to be public.")]
public sealed class AutorizacaoEndpointFixture : MonolitoPostgresFixture;
