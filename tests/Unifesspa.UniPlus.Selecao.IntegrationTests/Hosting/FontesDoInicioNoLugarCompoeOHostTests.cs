namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Hosting;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// As fontes de início no lugar registradas no host. Cada fonte nova entra na lista esperada junto do PR que a cria,
/// e omitir ou remover uma fonte quebra o teste.
/// </summary>
public sealed class FontesDoInicioNoLugarCompoeOHostTests : IClassFixture<SelecaoApiFactory>
{
    private static readonly Type[] FontesEsperadas = [];

    private readonly SelecaoApiFactory _factory;

    public FontesDoInicioNoLugarCompoeOHostTests(SelecaoApiFactory factory) => _factory = factory;

    [Fact(DisplayName = "O host registra exatamente as fontes de início no lugar esperadas")]
    public void Host_RegistraExatamenteAsFontesEsperadas()
    {
        using AsyncServiceScope escopo = _factory.Services.CreateAsyncScope();

        IEnumerable<IFonteDoInicioNoLugar> fontes = escopo.ServiceProvider.GetServices<IFonteDoInicioNoLugar>();

        fontes.Select(static f => f.GetType()).Should().BeEquivalentTo(FontesEsperadas);
    }
}
