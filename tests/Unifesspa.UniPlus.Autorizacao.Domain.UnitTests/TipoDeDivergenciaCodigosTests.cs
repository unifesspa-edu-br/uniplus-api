namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TipoDeDivergenciaCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDeDivergencia>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<TipoDeDivergencia, string>
            {
                [TipoDeDivergencia.ComposicaoFaltante] = "COMPOSICAO_FALTANTE",
                [TipoDeDivergencia.ComposicaoExcedente] = "COMPOSICAO_EXCEDENTE",
                [TipoDeDivergencia.Classificacao] = "CLASSIFICACAO",
                [TipoDeDivergencia.Escopo] = "ESCOPO",
            });
}
