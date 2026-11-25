namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDaMigracaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<SituacaoDaMigracao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDaMigracao, string>
            {
                [SituacaoDaMigracao.Retida] = "RETIDA",
                [SituacaoDaMigracao.AguardandoProtecaoDeDados] = "AGUARDANDO_PROTECAO_DE_DADOS",
                [SituacaoDaMigracao.Migrada] = "MIGRADA",
                [SituacaoDaMigracao.Recusada] = "RECUSADA",
                [SituacaoDaMigracao.NaoMigrada] = "NAO_MIGRADA",
                [SituacaoDaMigracao.ForaDaLista] = "FORA_DA_LISTA",
            });
}
