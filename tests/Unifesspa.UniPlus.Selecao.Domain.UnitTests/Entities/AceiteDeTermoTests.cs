namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class AceiteDeTermoTests
{
    [Fact]
    public void ValeParaAVersao_MesmaVersaoEMesmoHash_Vale()
    {
        var aceite = new AceiteDeTermo("A", Guid.Empty, "abc");
        aceite.ValeParaAVersao(new TermoAdmitido(Guid.Empty, "abc")).Should().BeTrue();
    }

    [Fact]
    public void ValeParaAVersao_MesmaVersaoEHashDiferente_NaoVale()
    {
        var aceite = new AceiteDeTermo("A", Guid.Empty, "abc");
        aceite.ValeParaAVersao(new TermoAdmitido(Guid.Empty, "outro")).Should().BeFalse();
    }

    [Fact]
    public void ValeParaAVersao_MesmoHashEVersaoDiferente_NaoVale()
    {
        var aceite = new AceiteDeTermo("A", Guid.Empty, "abc");
        aceite.ValeParaAVersao(new TermoAdmitido(Guid.NewGuid(), "abc")).Should().BeFalse();
    }

    [Fact]
    public void ValeParaAVersao_HashDiferenteApenasNaCaixa_NaoVale()
    {
        var aceite = new AceiteDeTermo("A", Guid.Empty, "abc");
        aceite.ValeParaAVersao(new TermoAdmitido(Guid.Empty, "ABC")).Should().BeFalse();
    }
}
