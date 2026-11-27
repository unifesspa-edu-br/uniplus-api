namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

public sealed class LugarDaDeclaracaoTests
{
    [Fact(DisplayName = "A fase é dita pelo nome")]
    public void DaFase_DizONome()
    {
        LugarDaDeclaracao.DaFase(new CitacaoDaFase("Divulgação do resultado")).Should().Be("na fase 'Divulgação do resultado'");
    }

    [Fact(DisplayName = "A etapa é dita pelo nome e pela ordem, e sem ordem diz que não foi definida")]
    public void DaEtapa_DizONomeEAOrdem()
    {
        LugarDaDeclaracao.DaEtapa("Redação", 3).Should().Be("na etapa 'Redação' (ordem 3)");
        LugarDaDeclaracao.DaEtapa("Redação", null).Should().Be("na etapa 'Redação' (ordem não definida)");
    }
}
