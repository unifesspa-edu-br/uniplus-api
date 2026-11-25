namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

public sealed class CaminhosDasFasesEEtapasTests
{
    [Theory]
    [InlineData(null, "fases[2]", "etapas[2]")]
    [InlineData("", "fases[2]", "etapas[2]")]
    [InlineData("nome", "fases[2].nome", "etapas[2].nome")]
    [InlineData("[0].nome", "fases[2][0].nome", "etapas[2][0].nome")]
    public void Caminhos_CausaRelativa_SituamNoItem(string? campo, string fase, string etapa)
    {
        DomainError erro = new("codigo", "mensagem");

        CronogramaDoProcesso.NaFase(2, campo, erro).Field.Should().Be(fase);
        EtapasDoProcesso.NaEtapa(2, campo, erro).Field.Should().Be(etapa);
    }
}
