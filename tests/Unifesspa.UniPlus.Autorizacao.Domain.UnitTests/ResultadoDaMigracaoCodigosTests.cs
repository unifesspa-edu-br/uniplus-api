namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ResultadoDaMigracaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<ResultadoDaMigracao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<ResultadoDaMigracao, string>
            {
                [ResultadoDaMigracao.Migrar] = "MIGRAR",
                [ResultadoDaMigracao.Recusar] = "RECUSAR",
                [ResultadoDaMigracao.Reter] = "RETER",
                [ResultadoDaMigracao.NaoMigrada] = "NAO_MIGRADA",
                [ResultadoDaMigracao.ForaDaLista] = "FORA_DA_LISTA",
            });
}
