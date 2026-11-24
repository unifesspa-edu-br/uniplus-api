namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ClassificacoesDeProtecaoTests
{
    [Theory(DisplayName = "A classificação que não se resolve ou está fora do roster vale como sensível; as demais valem por si")]
    [InlineData(null, ClassificacaoDeProtecao.Sensivel)]
    [InlineData(default(ClassificacaoDeProtecao), ClassificacaoDeProtecao.Sensivel)]
    [InlineData((ClassificacaoDeProtecao)99, ClassificacaoDeProtecao.Sensivel)]
    [InlineData(ClassificacaoDeProtecao.Publico, ClassificacaoDeProtecao.Publico)]
    [InlineData(ClassificacaoDeProtecao.Identificador, ClassificacaoDeProtecao.Identificador)]
    [InlineData(ClassificacaoDeProtecao.Sensivel, ClassificacaoDeProtecao.Sensivel)]
    public void Resolvida_AplicaOFallbackSensivel(ClassificacaoDeProtecao? entrada, ClassificacaoDeProtecao esperada) =>
        ClassificacoesDeProtecao.Resolvida(entrada).Should().Be(esperada);
    [Theory]
    [InlineData(ClassificacaoDeProtecao.Publico, "PUBLICO", "Público")]
    [InlineData(ClassificacaoDeProtecao.Interno, "INTERNO", "Interno")]
    [InlineData(ClassificacaoDeProtecao.Pessoal, "PESSOAL", "Pessoal")]
    [InlineData(ClassificacaoDeProtecao.Identificador, "IDENTIFICADOR", "Identificador")]
    [InlineData(ClassificacaoDeProtecao.Sensivel, "SENSIVEL", "Sensível")]
    public void Codigo_ValorDeclarado_DevolveCodigoERotulo(ClassificacaoDeProtecao valor, string codigo, string rotulo)
    {
        valor.Codigo().Should().Be(codigo);
        valor.Rotulo().Should().Be(rotulo);
        CatalogoDe<ClassificacaoDeProtecao>.TentarObter(codigo, out ClassificacaoDeProtecao lido).Should().BeTrue();
        lido.Should().Be(valor);
    }

    [Theory]
    [InlineData("publico")]
    [InlineData("NENHUMA")]
    [InlineData(" PUBLICO ")]
    [InlineData(null)]
    public void TryAnalisar_CodigoForaDoContrato_Recusa(string? codigo) =>
        ClassificacoesDeProtecao.TryAnalisar(codigo, out _).Should().BeFalse();
}
