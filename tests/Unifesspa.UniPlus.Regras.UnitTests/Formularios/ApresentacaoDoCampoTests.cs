namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Como o campo se apresenta ao candidato: rótulo, tipo de campo, ajuda e confirmação.</summary>
public sealed class ApresentacaoDoCampoTests
{
    [Theory(DisplayName = "O tipo de campo que não é membro do enum é defeito de quem chama")]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Construtor_TipoForaDoEnum_Lanca(int valor)
    {
        Action criar = () => _ = new ApresentacaoDoCampo("Nome", (TipoDeCampo)valor);

        criar.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("tipoDeCampo");
    }

    [Fact(DisplayName = "Sem ajuda e sem pedido de confirmação, o padrão é nenhum dos dois")]
    public void Construtor_SoComRotuloETipo_SemAjudaESemConfirmacao()
    {
        ApresentacaoDoCampo apresentacao = new("Nome", TipoDeCampo.Texto);

        apresentacao.Ajuda.Should().BeNull();
        apresentacao.PedirConfirmacao.Should().BeFalse();
    }

    [Fact(DisplayName = "Na forma gravada, o rótulo e a ajuda saem aparados, e o resto fica como veio")]
    public void NaFormaGravada_RotuloEAjudaComEspacos_Aparados()
    {
        ApresentacaoDoCampo informada = new("  Nome completo ", TipoDeCampo.Texto, "  Como no documento  ", pedirConfirmacao: true);

        ApresentacaoDoCampo gravada = informada.NaFormaGravada();

        gravada.Should().Be(new ApresentacaoDoCampo("Nome completo", TipoDeCampo.Texto, "Como no documento", pedirConfirmacao: true));
    }

    [Theory(DisplayName = "Na forma gravada, a ajuda vazia vira ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NaFormaGravada_AjudaVazia_ViraNula(string? ajuda) =>
        new ApresentacaoDoCampo("Nome", TipoDeCampo.Texto, ajuda).NaFormaGravada().Ajuda.Should().BeNull();
}
