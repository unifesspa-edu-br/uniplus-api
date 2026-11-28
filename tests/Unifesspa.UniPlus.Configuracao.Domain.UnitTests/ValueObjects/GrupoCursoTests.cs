namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class GrupoCursoTests
{
    [Fact(DisplayName = "Os quatro grupos têm código e rótulo fixos da Resolução nº 805/2024/Consepe, na ordem de exibição")]
    public void Todos_CodigoERotuloNaOrdem()
    {
        GrupoCurso.Todos.Select(g => (g.Codigo, g.Rotulo)).Should().Equal(
            ("TECNOLOGICA", "Tecnológica"),
            ("HUMANISTICA_I", "Humanística I"),
            ("HUMANISTICA_II", "Humanística II"),
            ("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"));
    }

    [Theory(DisplayName = "Criar aceita o código de cada grupo e põe o rótulo do sistema")]
    [InlineData(GrupoCurso.Tecnologica, "Tecnológica")]
    [InlineData(GrupoCurso.HumanisticaI, "Humanística I")]
    [InlineData(GrupoCurso.HumanisticaII, "Humanística II")]
    [InlineData(GrupoCurso.SaudeEBiologicas, "Saúde e Biológicas")]
    public void Criar_Codigo_AceitaComRotulo(string codigo, string rotulo)
    {
        Result<GrupoCurso> resultado = GrupoCurso.Criar(codigo);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Codigo.Should().Be(codigo);
        resultado.Value.Rotulo.Should().Be(rotulo);
    }

    [Theory(DisplayName = "Criar recusa o que não é código de um dos quatro grupos, inclusive o rótulo")]
    [InlineData("Tecnológica")]
    [InlineData("Humanística I")]
    [InlineData("tecnologica")]
    [InlineData("Tecnologica")]
    [InlineData("  TECNOLOGICA  ")]
    [InlineData(" HUMANISTICA_I")]
    [InlineData("SAUDE_E_BIOLOGICAS ")]
    [InlineData("ENGENHARIAS")]
    [InlineData("HUMANISTICA_III")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_ForaDoDominio_Falha(string? codigo)
    {
        Result<GrupoCurso> resultado = GrupoCurso.Criar(codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GrupoCursoErrorCodes.ForaDoDominio);
    }

    [Fact(DisplayName = "A recusa lista os códigos com o rótulo ao lado e não atribui a resolução ao INEP")]
    public void Criar_ForaDoDominio_MensagemListaCodigosERotulos()
    {
        string mensagem = GrupoCurso.Criar("ENGENHARIAS").Error!.Message;

        mensagem.Should().Contain("TECNOLOGICA (Tecnológica)");
        mensagem.Should().Contain("SAUDE_E_BIOLOGICAS (Saúde e Biológicas)");
        mensagem.Should().NotContain("INEP");
    }
}
