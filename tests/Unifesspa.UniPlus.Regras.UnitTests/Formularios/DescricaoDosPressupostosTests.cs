namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

public sealed class DescricaoDosPressupostosTests
{
    private static readonly IReadOnlyDictionary<string, ApresentacaoDoFato> SemCatalogo = new Dictionary<string, ApresentacaoDoFato>();

    [Fact(DisplayName = "O fato perguntado por outro formulário traz a apresentação dele, sem de quais fatos é calculado")]
    public void Descrever_FatoPerguntadoPorOutroFormulario_TrazAApresentacaoDele()
    {
        ValorSelecionavel preta = new("PRETA", "Preta", 0);
        DescricaoDosPressupostos descricao = new([Campo("COR_RACA", "Cor ou raça", "ESCOLHA_UNICA", valores: [preta])], SemCatalogo, []);

        IReadOnlyList<PressupostoRenderizavel> pressupostos = descricao.Descrever(["COR_RACA"]);

        pressupostos.Should().BeEquivalentTo([new PressupostoRenderizavel("COR_RACA", "Cor ou raça", "ESCOLHA_UNICA", null, [preta], null)], static opcoes => opcoes.WithStrictOrdering());
    }

    [Fact(DisplayName = "O fato que nenhum formulário pergunta traz só o rótulo e o tipo do catálogo")]
    public void Descrever_FatoSoNoCatalogo_TrazORotuloEOTipoDoCatalogo()
    {
        Dictionary<string, ApresentacaoDoFato> catalogo = new() { ["RENDA"] = new ApresentacaoDoFato("RENDA", "Renda per capita", "NUMERO") };
        DescricaoDosPressupostos descricao = new([], catalogo, []);

        IReadOnlyList<PressupostoRenderizavel> pressupostos = descricao.Descrever(["RENDA", "DESCONHECIDO"]);

        pressupostos.Should().BeEquivalentTo([
            new PressupostoRenderizavel("RENDA", "Renda per capita", "NUMERO", null, null, null),
            new PressupostoRenderizavel("DESCONHECIDO", null, null, null, null, null)], static opcoes => opcoes.WithStrictOrdering());
    }

    [Fact(DisplayName = "O fato calculado pelo sistema diz de quais fatos é calculado")]
    public void Descrever_FatoCalculadoDoSistema_DizDeQuaisFatosECalculado()
    {
        DescricaoDosPressupostos descricao = new([], SemCatalogo, []);

        IReadOnlyList<PressupostoRenderizavel> pressupostos = descricao.Descrever(["FAIXA_ETARIA"]);

        pressupostos.Should().BeEquivalentTo([new PressupostoRenderizavel("FAIXA_ETARIA", null, null, null, null, ["DATA_NASCIMENTO"])], static opcoes => opcoes.WithStrictOrdering());
    }

    [Fact(DisplayName = "O agregado sobre o grupo de outro formulário diz que é calculado do fato de membro que agrega")]
    public void Descrever_AgregadoSobreGrupo_DizDoFatoDeMembro()
    {
        DefinicaoAgregado agregado = new("RURAL_NA_FAMILIA", "FAMILIA", "TRABALHADOR_RURAL", OperacaoAgregado.Existe);
        DescricaoDosPressupostos descricao = new([], SemCatalogo, [agregado]);

        IReadOnlyList<PressupostoRenderizavel> pressupostos = descricao.Descrever(["RURAL_NA_FAMILIA"]);

        pressupostos.Should().BeEquivalentTo([new PressupostoRenderizavel("RURAL_NA_FAMILIA", null, null, null, null, ["TRABALHADOR_RURAL"])], static opcoes => opcoes.WithStrictOrdering());
    }

    [Fact(DisplayName = "O fato perguntado por dois outros formulários vale pelo primeiro campo, na ordem dada")]
    public void Descrever_FatoPerguntadoDuasVezes_ValePeloPrimeiroCampo()
    {
        DescricaoDosPressupostos descricao = new(
            [Campo("COR_RACA", "Primeiro rótulo", "ESCOLHA_UNICA"), Campo("COR_RACA", "Segundo rótulo", "TEXTO")], SemCatalogo, []);

        IReadOnlyList<PressupostoRenderizavel> pressupostos = descricao.Descrever(["COR_RACA"]);

        pressupostos.Should().BeEquivalentTo([new PressupostoRenderizavel("COR_RACA", "Primeiro rótulo", "ESCOLHA_UNICA", null, null, null)], static opcoes => opcoes.WithStrictOrdering());
    }

    private static CampoRenderizavel Campo(string fato, string rotulo, string tipo, IReadOnlyList<ValorSelecionavel>? valores = null) =>
        new(fato, 0, null, new ApresentacaoDoCampoDto(rotulo, tipo, null, false), null, valores);
}
