namespace Unifesspa.UniPlus.Host.IntegrationTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Tempo;

/// <summary>
/// O instante que o host grava, compara e publica sai de um relógio só: o do sistema, em microssegundos, como o
/// banco o persiste. Um segundo registro, de qualquer extensão ou biblioteca, reintroduziria o sétimo dígito.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class RelogioUnicoNoHostTests(MonolitoPostgresFixture fixture)
{
    [Fact(DisplayName = "A composição completa do host tem um único relógio, o do sistema em microssegundos")]
    public void ComposicaoCompleta_TemUmUnicoRelogioDoSistemaEmMicrossegundos()
    {
        IEnumerable<TimeProvider> relogios = fixture.Factory.Services.GetServices<TimeProvider>();

        relogios.Should().ContainSingle().Which.Should().BeSameAs(RelogioEmMicrossegundos.DoSistema);
    }
}
