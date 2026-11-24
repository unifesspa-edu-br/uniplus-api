namespace Unifesspa.UniPlus.ArchTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDeModalidadeEntreModulosTests
{
    [Fact]
    public void NaturezaLegal_CodigosDaConfiguracao_SaoLidosPelaSelecao() =>
        ConferirCodigos<NaturezaLegal, NaturezaLegalModalidade>();

    [Fact]
    public void ComposicaoVagas_CodigosDaConfiguracao_SaoLidosPelaSelecao() =>
        ConferirCodigos<ComposicaoVagas, ComposicaoVagasModalidade>();

    [Fact]
    public void RegraRemanejamento_CodigosDaConfiguracao_SaoLidosPelaSelecao() =>
        ConferirCodigos<RegraRemanejamento, RegraRemanejamentoModalidade>();

    private static void ConferirCodigos<TConfiguracao, TSelecao>()
        where TConfiguracao : struct, Enum
        where TSelecao : struct, Enum
    {
        foreach (TConfiguracao valor in CatalogoDe<TConfiguracao>.Valores())
        {
            string codigo = valor.Codigo();
            CatalogoDe<TSelecao>.TentarObter(codigo, out _).Should().BeTrue($"o código {codigo} é enviado pela Configuração");
        }
    }
}
