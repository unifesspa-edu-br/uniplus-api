namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>A forma do item lida da entrada, conferida mesmo quando alguma parte dele não foi lida.</summary>
public sealed class FormaDoItemNaEntradaTests
{
    private static readonly RegrasLidasDoCampo SemRegras = new(null, Obrigatoriedade.Sempre, [], null);

    [Fact(DisplayName = "O item bem formado não é recusado")]
    public void Conferir_ItemBemFormado_SemRecusa() =>
        FormaDoItemNaEntrada.Conferir(Item(), TipoDeCampo.Texto, "NOME_PESSOA", SemRegras).Should().BeEmpty();

    [Fact(DisplayName = "O código, a ordem e o rótulo recusados vêm no caminho da forma básica")]
    public void Conferir_FormaBasicaInvalida_RecusaCodigoOrdemERotulo()
    {
        FatoColetadoInput entrada = new(" ", -1, new ApresentacaoDoCampoInput(" ", "TEXTO"), null);

        FormaDoItemNaEntrada.Conferir(entrada, TipoDeCampo.Texto, "NOME_PESSOA", SemRegras).Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("fatoCodigo", ItemFormularioErrorCodes.FatoCodigoObrigatorio),
            ("ordem", ItemFormularioErrorCodes.OrdemInvalida),
            ("apresentacao.rotulo", ItemFormularioErrorCodes.RotuloObrigatorio));
    }

    [Fact(DisplayName = "A apresentação ausente recusa o rótulo e não impede conferir o resto")]
    public void Conferir_ApresentacaoAusente_RecusaSoORotulo()
    {
        FatoColetadoInput entrada = new("CAMPO", 0, null, null);

        FormaDoItemNaEntrada.Conferir(entrada, null, null, SemRegras).Should().ContainSingle()
            .Which.Field.Should().Be("apresentacao.rotulo");
    }

    [Fact(DisplayName = "A ajuda acima do limite é recusada no caminho da apresentação")]
    public void Conferir_AjudaLonga_RecusaNaApresentacao()
    {
        FieldError erro = FormaDoItemNaEntrada.Conferir(Item(new string('a', 1001)), TipoDeCampo.Texto, "NOME_PESSOA", SemRegras).Should().ContainSingle().Subject;

        erro.Field.Should().Be("apresentacao.ajuda");
        erro.Error.Code.Should().Be(ItemFormularioErrorCodes.AjudaTamanho);
    }

    [Fact(DisplayName = "O campo de texto sem formato é recusado")]
    public void Conferir_TextoSemFormato_RecusaOFormato() =>
        FormaDoItemNaEntrada.Conferir(Item(), TipoDeCampo.Texto, null, SemRegras).Should().ContainSingle()
            .Which.Error.Code.Should().Be(ItemFormularioErrorCodes.FormatoIncoerente);

    [Fact(DisplayName = "Sem o tipo de campo, o formato não é conferido")]
    public void Conferir_TipoNaoLido_NaoConfereOFormato() =>
        FormaDoItemNaEntrada.Conferir(Item(), null, null, SemRegras).Should().BeEmpty();

    [Fact(DisplayName = "As regras são conferidas no caminho das regras")]
    public void Conferir_RestricaoForaDoTipo_RecusaNoCaminhoDasRegras()
    {
        RegrasLidasDoCampo regras = new(null, Obrigatoriedade.Sempre, [new TamanhoTexto(1, 10)], null);

        FormaDoItemNaEntrada.Conferir(Item(), TipoDeCampo.Numero, null, regras).Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("regras.restricoes[0]", ItemFormularioErrorCodes.RestricaoIncoerente));
    }

    [Fact(DisplayName = "A obrigatoriedade não lida conta como conforme")]
    public void Conferir_ObrigatoriedadeNaoLida_SemRecusa() =>
        FormaDoItemNaEntrada.Conferir(Item(), TipoDeCampo.Texto, "NOME_PESSOA", new RegrasLidasDoCampo(null, null, [], null)).Should().BeEmpty();

    private static FatoColetadoInput Item(string? ajuda = null) =>
        new("CAMPO", 0, new ApresentacaoDoCampoInput("Nome", "TEXTO", ajuda), null);
}
