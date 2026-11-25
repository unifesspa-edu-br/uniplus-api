namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>A identificação da etapa normalizada: o que a etapa e a declaração dela guardam.</summary>
public sealed class IdentificacaoDaEtapaTests
{
    private static TipoEtapaSnapshot Tipo() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    [Fact(DisplayName = "O nome é aparado e o código da fase é preservado")]
    public void Normalizada_ComEspacos_PreservaCodigo()
    {
        TipoEtapaSnapshot tipo = Tipo();
        IdentificacaoDaEtapa identificacao = new("  Prova objetiva ", CaraterEtapa.Ambas, tipo, 2, " AVALIACAO ");

        IdentificacaoDaEtapa normalizada = identificacao.Normalizada();

        normalizada.Should().Be(new IdentificacaoDaEtapa("Prova objetiva", CaraterEtapa.Ambas, tipo, 2, " AVALIACAO "));
    }

    [Theory(DisplayName = "O código da fase em branco vira ausente")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Normalizada_CodigoDaFaseEmBranco_ViraAusente(string? codigo)
    {
        IdentificacaoDaEtapa identificacao = new("Prova", CaraterEtapa.Classificatoria, Tipo(), null, codigo);

        identificacao.Normalizada().FaseCodigo.Should().BeNull();
    }
}
