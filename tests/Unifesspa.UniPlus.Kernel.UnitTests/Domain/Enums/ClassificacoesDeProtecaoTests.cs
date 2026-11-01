namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ClassificacoesDeProtecaoTests
{
    [Theory(DisplayName = "A classificação que não se resolve ou está fora do roster vale como sensível; as demais valem por si")]
    [InlineData(ClassificacaoDeProtecao.Nenhuma, ClassificacaoDeProtecao.Sensivel)]
    [InlineData((ClassificacaoDeProtecao)99, ClassificacaoDeProtecao.Sensivel)]
    [InlineData(ClassificacaoDeProtecao.Publico, ClassificacaoDeProtecao.Publico)]
    [InlineData(ClassificacaoDeProtecao.Identificador, ClassificacaoDeProtecao.Identificador)]
    [InlineData(ClassificacaoDeProtecao.Sensivel, ClassificacaoDeProtecao.Sensivel)]
    public void Resolvida_AplicaOFallbackSensivel(ClassificacaoDeProtecao entrada, ClassificacaoDeProtecao esperada) =>
        ClassificacoesDeProtecao.Resolvida(entrada).Should().Be(esperada);
}
