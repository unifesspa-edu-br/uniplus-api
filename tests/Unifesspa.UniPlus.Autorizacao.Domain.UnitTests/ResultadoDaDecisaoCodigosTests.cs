namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ResultadoDaDecisaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<ResultadoDaDecisao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<ResultadoDaDecisao, string>
            {
                [ResultadoDaDecisao.Aprovado] = "APROVADO",
                [ResultadoDaDecisao.Recusado] = "RECUSADO",
                [ResultadoDaDecisao.PerfilAlterado] = "PERFIL_ALTERADO",
                [ResultadoDaDecisao.PrazoVencido] = "PRAZO_VENCIDO",
                [ResultadoDaDecisao.TermoExigido] = "TERMO_EXIGIDO",
            });
}
