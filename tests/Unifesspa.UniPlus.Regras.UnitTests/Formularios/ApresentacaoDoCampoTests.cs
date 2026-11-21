namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
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
        Action criar = () => _ = new ApresentacaoDoCampo("Nome", (TipoRenderizacao)valor);

        criar.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("tipoRenderizacao");
    }

    [Fact(DisplayName = "Sem ajuda e sem pedido de confirmação, o padrão é nenhum dos dois")]
    public void Construtor_SoComRotuloETipo_SemAjudaESemConfirmacao()
    {
        ApresentacaoDoCampo apresentacao = new("Nome", TipoRenderizacao.Texto);

        apresentacao.Ajuda.Should().BeNull();
        apresentacao.PedirConfirmacao.Should().BeFalse();
    }

    [Fact(DisplayName = "Na forma gravada, o rótulo e a ajuda saem aparados, e o resto fica como veio")]
    public void NaFormaGravada_RotuloEAjudaComEspacos_Aparados()
    {
        ApresentacaoDoCampo informada = new("  Nome completo ", TipoRenderizacao.Texto, "  Como no documento  ", pedirConfirmacao: true);

        ApresentacaoDoCampo gravada = informada.NaFormaGravada();

        gravada.Should().Be(new ApresentacaoDoCampo("Nome completo", TipoRenderizacao.Texto, "Como no documento", pedirConfirmacao: true));
    }

    [Theory(DisplayName = "Na forma gravada, a ajuda vazia vira ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NaFormaGravada_AjudaVazia_ViraNula(string? ajuda) =>
        new ApresentacaoDoCampo("Nome", TipoRenderizacao.Texto, ajuda).NaFormaGravada().Ajuda.Should().BeNull();

    [Theory(DisplayName = "A ajuda tem até 1000 caracteres")]
    [InlineData(1000, 0)]
    [InlineData(1001, 1)]
    public void ConferirAjuda_Tamanho_RecusaSoAcimaDoLimite(int tamanho, int recusas)
    {
        List<FieldError> erros = [.. new ApresentacaoDoCampo("Nome", TipoRenderizacao.Texto, new string('a', tamanho)).ConferirAjuda()];

        erros.Should().HaveCount(recusas);
        erros.Select(static e => (e.Field, e.Error.Code)).Should().OnlyContain(e => e.Field == "ajuda" && e.Code == ItemFormularioErrorCodes.AjudaTamanho);
    }

    [Fact(DisplayName = "O limite da ajuda conta o texto aparado")]
    public void ConferirAjuda_EspacosNasPontas_NaoContam() =>
        new ApresentacaoDoCampo("Nome", TipoRenderizacao.Texto, $"  {new string('a', 1000)}  ").ConferirAjuda().Should().BeEmpty();
}
