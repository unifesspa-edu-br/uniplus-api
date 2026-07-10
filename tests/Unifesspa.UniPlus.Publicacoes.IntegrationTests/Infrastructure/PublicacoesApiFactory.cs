namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Factory HTTP-only (Wolverine + migrations desabilitados) usada pelas suítes que
/// só exercitam o pipeline HTTP/OpenAPI sem Postgres real. Sobe a API UniPlus
/// (composition root); Publicações é uma library exercitada através dela.
/// </summary>
/// <remarks>
/// A connection string é dummy (não-vazia): em HTTP-only o DbContext é resolvido
/// lazy e nunca usado. Suítes que tocam o banco usam <see cref="PublicacoesEndpointFixture"/>.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x IClassFixture<T> requires the fixture type to be public.")]
public sealed class PublicacoesApiFactory : MonolitoApiFactory
{
    public PublicacoesApiFactory()
        : base(
            "Host=localhost;Port=5432;Database=uniplus;Username=uniplus;Password=uniplus_dev",
            wolverineEnabled: false)
    {
    }
}
