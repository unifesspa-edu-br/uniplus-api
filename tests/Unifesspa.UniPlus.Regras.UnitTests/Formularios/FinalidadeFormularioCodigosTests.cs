namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;

public sealed class FinalidadeFormularioCodigosTests
{
    [Fact(DisplayName = "Cada finalidade do formulário tem o código e o rótulo do contrato")]
    public void FinalidadeFormulario_CadaValor_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<FinalidadeFormulario>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<FinalidadeFormulario, (string, string)>
            {
                [FinalidadeFormulario.Inscricao] = ("INSCRICAO", "Inscrição"),
                [FinalidadeFormulario.IsencaoTaxa] = ("ISENCAO_TAXA", "Isenção da taxa"),
                [FinalidadeFormulario.Habilitacao] = ("HABILITACAO", "Habilitação"),
            });

    [Fact(DisplayName = "As finalidades seguem a ordem de declaração e o código desconhecido ou ausente não é finalidade")]
    public void FinalidadeFormulario_Catalogo_SegueAOrdemDeDeclaracaoERecusaCodigoDesconhecido()
    {
        CatalogoDe<FinalidadeFormulario>.Valores().Should().Equal(
            FinalidadeFormulario.Inscricao, FinalidadeFormulario.IsencaoTaxa, FinalidadeFormulario.Habilitacao);
        EstruturaFormulario.FinalidadeDoCodigo("HABILITACAO").Should().Be(FinalidadeFormulario.Habilitacao);
        EstruturaFormulario.FinalidadeDoCodigo("habilitacao").Should().BeNull();
        EstruturaFormulario.FinalidadeDoCodigo("1").Should().BeNull();
        EstruturaFormulario.FinalidadeDoCodigo(null).Should().BeNull();
    }
}
