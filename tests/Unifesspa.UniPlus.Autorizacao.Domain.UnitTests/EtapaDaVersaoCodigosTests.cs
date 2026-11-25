namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class EtapaDaVersaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<EtapaDaVersao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<EtapaDaVersao, string>
            {
                [EtapaDaVersao.GestaoDeAcessos] = "GESTAO_DE_ACESSOS",
                [EtapaDaVersao.ProtecaoDeDados] = "PROTECAO_DE_DADOS",
            });
}
