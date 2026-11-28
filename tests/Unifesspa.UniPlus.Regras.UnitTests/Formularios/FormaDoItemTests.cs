namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>A forma do item: a ajuda e a conferência da forma inteira, inclusive a lida do pedido com parte recusada.</summary>
public sealed class FormaDoItemTests
{
    private static readonly RegrasLidasDoCampo SemRegras = new(null, Obrigatoriedade.Sempre, [], null);

    [Theory(DisplayName = "A ajuda tem até 1000 caracteres")]
    [InlineData(1000, 0)]
    [InlineData(1001, 1)]
    public void RecusasDaAjuda_Tamanho_RecusaSoAcimaDoLimite(int tamanho, int recusas)
    {
        List<FieldError> erros = [.. FormaDoItem.RecusasDaAjuda(new string('a', tamanho))];

        erros.Should().HaveCount(recusas);
        erros.Should().OnlyContain(e => e.Field == "ajuda" && e.Error.Code == ItemFormularioErrorCodes.AjudaTamanho);
    }

    [Fact(DisplayName = "O limite da ajuda conta o texto aparado")]
    public void RecusasDaAjuda_EspacosNasPontas_NaoContam() =>
        FormaDoItem.RecusasDaAjuda($"  {new string('a', 1000)}  ").Should().BeEmpty();

    [Theory(DisplayName = "A ajuda ausente ou vazia não é recusada")]
    [InlineData(null)]
    [InlineData("   ")]
    public void RecusasDaAjuda_SemAjuda_SemRecusa(string? ajuda) =>
        FormaDoItem.RecusasDaAjuda(ajuda).Should().BeEmpty();

    [Fact(DisplayName = "O item bem formado não é recusado")]
    public void Conferir_ItemBemFormado_SemRecusa() =>
        Conferir(Item(), TipoDeCampo.Texto, "NOME_PESSOA", SemRegras).Should().BeEmpty();

    [Fact(DisplayName = "O código, a ordem e o rótulo recusados vêm no caminho da forma básica")]
    public void Conferir_FormaBasicaInvalida_RecusaCodigoOrdemERotulo()
    {
        FatoColetadoInput entrada = new(" ", -1, new ApresentacaoDoCampoInput(" ", "TEXTO"), null);

        Conferir(entrada, TipoDeCampo.Texto, "NOME_PESSOA", SemRegras).Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("fatoCodigo", ItemFormularioErrorCodes.FatoCodigoObrigatorio),
            ("ordem", ItemFormularioErrorCodes.OrdemInvalida),
            ("apresentacao.rotulo", ItemFormularioErrorCodes.RotuloObrigatorio));
    }

    [Fact(DisplayName = "A apresentação ausente recusa o rótulo e não impede conferir o resto")]
    public void Conferir_ApresentacaoAusente_RecusaSoORotulo()
    {
        FatoColetadoInput entrada = new("CAMPO", 0, null, null);

        Conferir(entrada, null, null, SemRegras).Should().ContainSingle()
            .Which.Field.Should().Be("apresentacao.rotulo");
    }

    [Fact(DisplayName = "A ajuda acima do limite é recusada no caminho da apresentação")]
    public void Conferir_AjudaLonga_RecusaNaApresentacao()
    {
        FieldError erro = Conferir(Item(new string('a', 1001)), TipoDeCampo.Texto, "NOME_PESSOA", SemRegras).Should().ContainSingle().Subject;

        erro.Field.Should().Be("apresentacao.ajuda");
        erro.Error.Code.Should().Be(ItemFormularioErrorCodes.AjudaTamanho);
    }

    [Fact(DisplayName = "O campo de texto sem formato é recusado")]
    public void Conferir_TextoSemFormato_RecusaOFormato() =>
        Conferir(Item(), TipoDeCampo.Texto, null, SemRegras).Should().ContainSingle()
            .Which.Error.Code.Should().Be(ItemFormularioErrorCodes.FormatoIncoerente);

    [Fact(DisplayName = "Sem o tipo de campo, o formato não é conferido")]
    public void Conferir_TipoNaoLido_NaoConfereOFormato() =>
        Conferir(Item(), null, null, SemRegras).Should().BeEmpty();

    [Fact(DisplayName = "Sem o tipo de campo, a ordem é conferida e a restrição fora do tipo e o formato não")]
    public void Conferir_TipoNaoLidoComOrdemNegativaERestricao_ConfereSoAOrdem()
    {
        FatoColetadoInput entrada = new("CAMPO", -1, new ApresentacaoDoCampoInput("Nome", "TEXTO", null), null);
        RegrasLidasDoCampo regras = new(null, Obrigatoriedade.Sempre, [new FaixaNumerica(1, 2)], null);

        Conferir(entrada, null, "FORMATO_QUALQUER", regras).Select(static e => e.Error.Code).Should().Equal(ItemFormularioErrorCodes.OrdemInvalida);
    }

    [Fact(DisplayName = "As regras são conferidas no caminho das regras")]
    public void Conferir_RestricaoForaDoTipo_RecusaNoCaminhoDasRegras()
    {
        RegrasLidasDoCampo regras = new(null, Obrigatoriedade.Sempre, [new TamanhoTexto(1, 10)], null);

        Conferir(Item(), TipoDeCampo.Numero, null, regras).Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("regras.restricoes[0]", ItemFormularioErrorCodes.RestricaoIncoerente));
    }

    [Fact(DisplayName = "A obrigatoriedade não lida conta como conforme")]
    public void Conferir_ObrigatoriedadeNaoLida_SemRecusa() =>
        Conferir(Item(), TipoDeCampo.Texto, "NOME_PESSOA", new RegrasLidasDoCampo(null, null, [], null)).Should().BeEmpty();

    private static FatoColetadoInput Item(string? ajuda = null) =>
        new("CAMPO", 0, new ApresentacaoDoCampoInput("Nome", "TEXTO", ajuda), null);

    private static List<FieldError> Conferir(FatoColetadoInput entrada, TipoDeCampo? tipo, string? formato, RegrasLidasDoCampo regras) =>
        FormaDoItem.Conferir(entrada.FatoCodigo, entrada.Ordem, ApresentacaoLidaDoCampo.DaEntrada(entrada.Apresentacao, tipo), formato, regras);
}
