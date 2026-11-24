namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class ConsequenciaDoIndeferimentoCodigosTests
{
    [Fact]
    public void ConsequenciaDoIndeferimento_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<ConsequenciaDoIndeferimento>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (ConsequenciaDoIndeferimento.Elimina, "ELIMINA", "Eliminação"),
                (ConsequenciaDoIndeferimento.ReclassificaAc, "RECLASSIFICA_AC", "Ampla concorrência"),
                (ConsequenciaDoIndeferimento.RemoveVantagem, "REMOVE_VANTAGEM", "Perda da vantagem"),
                (ConsequenciaDoIndeferimento.AbrePrazoParaReenvio, "ABRE_PRAZO_PARA_REENVIO", "Prazo para reenvio"));
}
