namespace Unifesspa.UniPlus.Discentes.UnitTests.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

/// <summary>
/// A matrícula e o nome identificam a pessoa: a regra de cada um, e nenhuma interpolação
/// os expõe.
/// </summary>
public sealed class MatriculaENomeDoDiscenteTests
{
    [Theory]
    [InlineData(null, "VinculoDiscente.MatriculaVazia")]
    [InlineData("", "VinculoDiscente.MatriculaVazia")]
    [InlineData("   ", "VinculoDiscente.MatriculaVazia")]
    [InlineData("12A", "VinculoDiscente.MatriculaNaoNumerica")]
    [InlineData(" 123", "VinculoDiscente.MatriculaNaoNumerica")]
    [InlineData("12 3", "VinculoDiscente.MatriculaNaoNumerica")]
    public void Matricula_recusa_a_primeira_regra_violada(string? texto, string codigo)
    {
        MatriculaDoDiscente.Criar(texto).Error!.Code.Should().Be(codigo);
    }

    [Fact]
    public void Matricula_aceita_o_limite_e_recusa_um_a_mais()
    {
        MatriculaDoDiscente.Criar(new string('9', LimitesDaReplica.Matricula)).IsSuccess.Should().BeTrue();
        MatriculaDoDiscente.Criar(new string('9', LimitesDaReplica.Matricula + 1)).Error!.Code
            .Should().Be("VinculoDiscente.MatriculaLonga");
    }

    [Fact]
    public void Matricula_nao_numerica_e_longa_e_recusada_como_nao_numerica()
    {
        MatriculaDoDiscente.Criar(new string('A', LimitesDaReplica.Matricula + 1)).Error!.Code
            .Should().Be("VinculoDiscente.MatriculaNaoNumerica");
    }

    [Fact]
    public void Matricula_guarda_os_zeros_a_esquerda_como_vieram()
    {
        MatriculaDoDiscente.Criar("00123").Value!.Valor.Should().Be("00123");
    }

    [Fact]
    public void Matricula_nao_aparece_em_texto()
    {
        MatriculaDoDiscente matricula = MatriculaDoDiscente.Criar("20142600001").Value!;

        $"{matricula}".Should().NotContain("20142600001");
    }

    [Theory]
    [InlineData("ANA EXEMPLO DE SOUZA", "A** E*** D* S***")]
    [InlineData("  ANA   EXEMPLO ", "A** E***")]
    [InlineData("X", "X")]
    public void Nome_so_aparece_mascarado_e_o_valor_persiste_como_veio(string texto, string mascarado)
    {
        NomeDoDiscente nome = NomeDoDiscente.Criar(texto).Value!;

        nome.ToString().Should().Be(mascarado);
        nome.Mascarado.Should().Be(mascarado);
        nome.Valor.Should().Be(texto);
    }

    [Fact]
    public void Nome_nao_exige_sobrenome_nem_restringe_caracteres()
    {
        NomeDoDiscente.Criar("ANA").IsSuccess.Should().BeTrue();
        NomeDoDiscente.Criar("ANA 2ª D'ÁVILA-SOUZA").IsSuccess.Should().BeTrue();
    }
}
