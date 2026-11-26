namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class ConsequenciaDoDescumprimentoCodigosTests
{
    [Fact]
    public void ConsequenciaDoDescumprimento_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<ConsequenciaDoDescumprimento>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (ConsequenciaDoDescumprimento.EliminaDoProcesso, "ELIMINA_DO_PROCESSO", "Eliminação do processo"),
                (ConsequenciaDoDescumprimento.RetiraAModalidade, "RETIRA_A_MODALIDADE", "Retirada da modalidade"),
                (ConsequenciaDoDescumprimento.RetiraOAcrescimo, "RETIRA_O_ACRESCIMO", "Retirada só do acréscimo do Fator de Atenuação das Desigualdades de Acesso"));
}
