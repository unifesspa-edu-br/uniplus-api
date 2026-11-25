namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class EtapaDaMigracaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<EtapaDaMigracao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<EtapaDaMigracao, string>
            {
                [EtapaDaMigracao.GestaoDeAcessos] = "GESTAO_DE_ACESSOS",
                [EtapaDaMigracao.ProtecaoDeDados] = "PROTECAO_DE_DADOS",
                [EtapaDaMigracao.Sistema] = "SISTEMA",
            });
}
