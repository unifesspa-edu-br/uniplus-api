namespace Unifesspa.UniPlus.Portal.IntegrationTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

/// <summary>
/// O instante que o Portal compara e publica sai de um relógio só: o do sistema, em microssegundos, o mesmo do host
/// do monólito. Um segundo registro, de qualquer extensão ou biblioteca, reintroduziria o sétimo dígito.
/// </summary>
public sealed class RelogioUnicoNoPortalTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    [Fact(DisplayName = "A composição completa do Portal tem um único relógio, o do sistema em microssegundos")]
    public void ComposicaoCompleta_TemUmUnicoRelogioDoSistemaEmMicrossegundos()
    {
        IEnumerable<TimeProvider> relogios = factory.Services.GetServices<TimeProvider>();

        relogios.Should().ContainSingle().Which.Should().BeSameAs(RelogioEmMicrossegundos.DoSistema);
    }
}
