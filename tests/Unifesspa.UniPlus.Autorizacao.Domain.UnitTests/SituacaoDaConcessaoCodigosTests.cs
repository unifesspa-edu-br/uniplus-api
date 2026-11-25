namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDaConcessaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaSituacao_EODeclarado() =>
        Enum.GetValues<SituacaoDaConcessao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDaConcessao, string>
            {
                [SituacaoDaConcessao.Ativa] = "ATIVA",
                [SituacaoDaConcessao.Revogada] = "REVOGADA",
                [SituacaoDaConcessao.Vencida] = "VENCIDA",
            });
}
