namespace Unifesspa.UniPlus.Verificador.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Kernel.Tempo;

/// <summary>
/// O instante que o verificador mede sai de um relógio só: o do sistema, em microssegundos, o mesmo da API. Um segundo
/// registro, de qualquer biblioteca, reintroduziria o sétimo dígito.
/// </summary>
public sealed class RelogioUnicoNoVerificadorTests
{
    [Fact(DisplayName = "A composição completa do verificador tem um único relógio, o do sistema em microssegundos")]
    public void ComposicaoCompleta_TemUmUnicoRelogioDoSistemaEmMicrossegundos()
    {
        using IHost host = PontoDeEntradaDoVerificador.ConstruirSemIniciar(PontoDeEntradaDoVerificador.ConfiguracaoMinima);

        IEnumerable<TimeProvider> relogios = host.Services.GetServices<TimeProvider>();

        relogios.Should().ContainSingle().Which.Should().BeSameAs(RelogioEmMicrossegundos.DoSistema);
    }
}
