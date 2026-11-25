namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDaVersaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<SituacaoDaVersao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDaVersao, string>
            {
                [SituacaoDaVersao.Pendente] = "PENDENTE",
                [SituacaoDaVersao.Vigente] = "VIGENTE",
                [SituacaoDaVersao.Encerrada] = "ENCERRADA",
                [SituacaoDaVersao.Cancelada] = "CANCELADA",
                [SituacaoDaVersao.Recusada] = "RECUSADA",
                [SituacaoDaVersao.Expirada] = "EXPIRADA",
            });
}
