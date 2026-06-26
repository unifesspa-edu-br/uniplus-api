namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Factory HTTP-only (Wolverine + migrations desabilitados) usada por suítes que
/// só exercitam o pipeline HTTP/OpenAPI sem Postgres real. Sobe a API UniPlus
/// (composition root); Selecao é uma library exercitada através dela.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x IClassFixture<T> requires the fixture type to be public.")]
public sealed class SelecaoApiFactory : MonolitoApiFactory
{
    public SelecaoApiFactory()
        : base(
            "Host=localhost;Port=5432;Database=uniplus;Username=uniplus;Password=uniplus_dev",
            wolverineEnabled: false)
    {
    }
}
