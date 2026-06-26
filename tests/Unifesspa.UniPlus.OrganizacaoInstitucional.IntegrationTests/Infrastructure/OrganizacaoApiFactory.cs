namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Factory HTTP-only (Wolverine + migrations desabilitados) usada por suítes que
/// só exercitam o pipeline HTTP/OpenAPI sem Postgres real. Sobe a API UniPlus
/// (composition root); OrganizacaoInstitucional é uma library exercitada através dela.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x IClassFixture<T> requires the fixture type to be public.")]
public sealed class OrganizacaoApiFactory : MonolitoApiFactory
{
    public OrganizacaoApiFactory()
        : base(
            "Host=localhost;Port=5432;Database=uniplus;Username=uniplus;Password=uniplus_dev",
            wolverineEnabled: false)
    {
    }
}
