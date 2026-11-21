namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A leitura do tipo de campo que o pedido informa para cada item e cada campo de grupo do modelo.</summary>
public sealed class TipoDeCampoDoItemDoModeloTests
{
    private static readonly Dictionary<string, string> SemFormatos = new(StringComparer.Ordinal);

    [Fact(DisplayName = "O código do tipo de campo é lido como o tipo no item do modelo")]
    public void Ler_CodigoDeclarado_ItemTemOTipo()
    {
        ConteudoLido lido = ConteudoLido.Ler(Conteudo(Item("SELECAO_MULTIPLA")), SemFormatos);

        lido.Erros.Should().BeEmpty();
        lido.Itens.Single().Item.Apresentacao.TipoRenderizacao.Should().Be(TipoRenderizacao.SelecaoMultipla);
    }

    [Fact(DisplayName = "Tipo de campo ausente é recusado como obrigatório e o item não é lido")]
    public void Ler_TipoAusente_RecusaComoObrigatorio()
    {
        ConteudoLido lido = ConteudoLido.Ler(Conteudo(Item(null)), SemFormatos);

        lido.Erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].apresentacao.tipoRenderizacao", "ItemFormulario.TipoRenderizacaoObrigatorio"));
        lido.Itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipo de campo desconhecido é recusado como inválido, também no campo de grupo")]
    public void Ler_TipoDesconhecido_RecusaComoInvalidoNoItemENoCampoDeGrupo()
    {
        GrupoColetadoInput grupo = new("DEPENDENTES", 1, "Dependentes", null, 0, null, null, "SEMPRE", null, [Item("NENHUMA")]);
        ConteudoLido lido = ConteudoLido.Ler(
            new ConteudoDoModeloInput("Habilitação", null, [Item("NENHUMA")], null, null, [grupo]), SemFormatos);

        lido.Erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].apresentacao.tipoRenderizacao", "ItemFormulario.TipoRenderizacaoInvalido"),
            ("grupos[0].subitens[0].apresentacao.tipoRenderizacao", "ItemFormulario.TipoRenderizacaoInvalido"));
    }

    [Fact(DisplayName = "Tipo de campo desconhecido acumula com a recusa da obrigatoriedade do mesmo item")]
    public void Ler_TipoDesconhecidoEObrigatoriedadeInvalida_AcumulaAsDuas()
    {
        FatoColetadoInput item = Campo("texto").ComObrigatoriedade("TALVEZ").Construir();

        ConteudoLido lido = ConteudoLido.Ler(Conteudo(item), SemFormatos);

        lido.Erros.Select(static e => e.Field).Should().Equal("itens[0].apresentacao.tipoRenderizacao", "itens[0].regras.obrigatoriedade");
    }

    [Fact(DisplayName = "A apresentação ausente é recusada pelo tipo de campo obrigatório, no caminho dela")]
    public void Ler_ApresentacaoAusente_RecusaOTipoNoCaminhoDaApresentacao()
    {
        ConteudoLido lido = ConteudoLido.Ler(Conteudo(Campo("BOOLEANO").SemApresentacao().Construir()), SemFormatos);

        lido.Erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].apresentacao.tipoRenderizacao", "ItemFormulario.TipoRenderizacaoObrigatorio"));
        lido.Itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "As regras ausentes são recusadas pela obrigatoriedade inválida, no caminho delas")]
    public void Ler_RegrasAusentes_RecusaAObrigatoriedadeNoCaminhoDasRegras()
    {
        ConteudoLido lido = ConteudoLido.Ler(Conteudo(Campo("BOOLEANO").SemRegras().Construir()), SemFormatos);

        lido.Erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].regras.obrigatoriedade", "ModeloFormulario.ObrigatoriedadeInvalida"));
        lido.Itens.Should().BeEmpty();
    }

    private static ConteudoDoModeloInput Conteudo(FatoColetadoInput item) =>
        new("Habilitação", null, [item], null, null);

    private static FatoColetadoInput Item(string? tipoRenderizacao) => Campo(tipoRenderizacao).Construir();

    private static EntradaDoFatoColetadoDeTeste Campo(string? tipoRenderizacao) =>
        EntradaDoFatoColetadoDeTeste.Coletando("CERTIFICADO").ComRotulo("Certificado").DoTipo(tipoRenderizacao);
}
