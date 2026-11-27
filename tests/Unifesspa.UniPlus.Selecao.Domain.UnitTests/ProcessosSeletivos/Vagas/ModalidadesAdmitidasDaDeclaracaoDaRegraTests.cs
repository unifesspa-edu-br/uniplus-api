namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Vagas;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

public sealed class ModalidadesAdmitidasDaDeclaracaoDaRegraTests
{
    [Fact(DisplayName = "O rol declarado é a lista de códigos de modalidade")]
    public void Extrair_RolDeclarado_DevolveOsCodigos() =>
        Extrair("""{"modalidades_admitidas":["AC","LB_PPI"]}""").Should().Equal("AC", "LB_PPI");

    [Fact(DisplayName = "O item do rol que não é texto fica fora")]
    public void Extrair_ItemQueNaoETexto_FicaFora() =>
        Extrair("""{"modalidades_admitidas":["AC",1,null,{"codigo":"LB_Q"}]}""").Should().Equal("AC");

    [Theory(DisplayName = "A declaração sem rol utilizável deixa o rol aberto, sem lançar exceção")]
    [InlineData("""[]""")]
    [InlineData("""{}""")]
    [InlineData("""{"modalidades_admitidas":null}""")]
    [InlineData("""{"modalidades_admitidas":"AC"}""")]
    [InlineData("""{"modalidades_admitidas":{"AC":true}}""")]
    public void Extrair_SemRolUtilizavel_RolAberto(string declaracao) =>
        Extrair(declaracao).Should().BeNull();

    private static IReadOnlyList<string>? Extrair(string declaracao)
    {
        using JsonDocument documento = JsonDocument.Parse(declaracao);

        return ModalidadesAdmitidasDaDeclaracaoDaRegra.Extrair(documento.RootElement);
    }
}
