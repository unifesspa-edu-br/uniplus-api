namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

public sealed class LugarDaDecisaoTests
{
    [Fact(DisplayName = "O lugar da fase guarda só a fase canônica de origem")]
    public void DaFase_GuardaSoAFase()
    {
        Guid origem = Guid.CreateVersion7();

        LugarDaDecisao lugar = LugarDaDecisao.DaFase(origem);

        lugar.FaseCanonicaOrigemId.Should().Be(origem);
        lugar.EtapaProcessoId.Should().BeNull();
    }

    [Fact(DisplayName = "O lugar da etapa guarda só a etapa do processo")]
    public void DaEtapa_GuardaSoAEtapa()
    {
        Guid etapa = Guid.CreateVersion7();

        LugarDaDecisao lugar = LugarDaDecisao.DaEtapa(etapa);

        lugar.EtapaProcessoId.Should().Be(etapa);
        lugar.FaseCanonicaOrigemId.Should().BeNull();
    }

    [Fact(DisplayName = "Dois lugares com a mesma identidade do mesmo tipo são iguais")]
    public void Igualdade_PorValor()
    {
        Guid id = Guid.CreateVersion7();

        LugarDaDecisao.DaFase(id).Should().Be(LugarDaDecisao.DaFase(id));
        LugarDaDecisao.DaEtapa(id).Should().Be(LugarDaDecisao.DaEtapa(id));
        LugarDaDecisao.DaFase(id).GetHashCode().Should().Be(LugarDaDecisao.DaFase(id).GetHashCode());
    }

    [Fact(DisplayName = "A fase e a etapa com o mesmo identificador são lugares diferentes")]
    public void FaseEEtapa_ComMesmoIdentificador_SaoDiferentes()
    {
        Guid id = Guid.CreateVersion7();

        LugarDaDecisao.DaFase(id).Should().NotBe(LugarDaDecisao.DaEtapa(id));
        LugarDaDecisao.DaFase(id).Should().NotBe(LugarDaDecisao.DaFase(Guid.CreateVersion7()));
    }

    [Fact(DisplayName = "A identidade vazia é recusada, na fase e na etapa")]
    public void IdentidadeVazia_Recusa()
    {
        Action daFase = () => LugarDaDecisao.DaFase(Guid.Empty);
        Action daEtapa = () => LugarDaDecisao.DaEtapa(Guid.Empty);

        daFase.Should().Throw<ArgumentException>().WithParameterName("faseCanonicaOrigemId");
        daEtapa.Should().Throw<ArgumentException>().WithParameterName("etapaProcessoId");
    }
}
