namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// A forma canônica do rótulo do tipo da entidade vinculada: o casamento é total, sem a quebra de
/// linha final que o <c>$</c> do .NET aceitaria e o do PostgreSQL recusa.
/// </summary>
public sealed class TipoDaEntidadeVinculadaTests
{
    [Theory(DisplayName = "Rótulo em maiúsculas e dígitos, em grupos separados por sublinhado, tem a forma canônica")]
    [InlineData("PROCESSO_SELETIVO")]
    [InlineData("CHAMADA")]
    [InlineData("APLICACAO_PROVA_2")]
    public void TemFormaCanonica_RotuloCanonico_Verdadeiro(string rotulo)
    {
        TipoDaEntidadeVinculada.TemFormaCanonica(rotulo).Should().BeTrue();
    }

    [Theory(DisplayName = "Rótulo com quebra de linha no fim não tem a forma canônica")]
    [InlineData("PROCESSO_SELETIVO\n")]
    [InlineData("PROCESSO_SELETIVO\r\n")]
    public void TemFormaCanonica_ComQuebraDeLinhaFinal_Falso(string rotulo)
    {
        TipoDaEntidadeVinculada.TemFormaCanonica(rotulo).Should().BeFalse();
    }

    [Theory(DisplayName = "Rótulo nulo, vazio ou fora da grafia canônica não tem a forma canônica")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("processo-seletivo")]
    [InlineData("PROCESSO__SELETIVO")]
    [InlineData(" PROCESSO_SELETIVO")]
    public void TemFormaCanonica_ForaDaGrafia_Falso(string? rotulo)
    {
        TipoDaEntidadeVinculada.TemFormaCanonica(rotulo).Should().BeFalse();
    }
}
