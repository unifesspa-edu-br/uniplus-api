namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class RecusasAcumuladasDoPredicadoNoJsonTests
{
    private const string CodigoDaForma = "CondicaoDnf.FormaIncoerenteComOperador";
    private const string MensagemDaForma = "Os operadores ALGUM_DE e NENHUM_DE exigem um array JSON, sem itens de texto em branco (array vazio é aceito).";
    private const string CondicaoInvalida = """{"codigoDoDado":"DADO_FICTICIO","operador":"ALGUM_DE","valor":"texto fictício"}""";

    [Fact]
    public void DeJson_CondicaoComDuasCausasSeguidaDeOutraInvalida_AcumulaTodasNaOrdem()
    {
        using JsonDocument documento = JsonDocument.Parse("""[[{"codigoDoDado":"","operador":"DESCONHECIDO","valor":true},{}]]""");
        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(documento.RootElement);

        lido.Errors.Select(static erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(
            ("[0][0].codigoDoDado", "CondicaoDnf.FatoObrigatorio", "O fato da condição é obrigatório."),
            ("[0][0].operador", "CondicaoDnf.OperadorInvalido", "O operador da condição não é reconhecido. Os operadores aceitos são IGUAL, ALGUM_DE, MAIOR_IGUAL, MENOR_IGUAL, DIFERENTE, NENHUM_DE."),
            ("[0][1].codigoDoDado", PredicadoDnfJson.FormaInvalida, "Cada condição tem codigoDoDado, operador e valor."));
    }

    [Fact]
    public void DeJson_DuasCondicoesInvalidas_AcumulaNaOrdem()
    {
        using JsonDocument documento = JsonDocument.Parse($"[[{CondicaoInvalida},{CondicaoInvalida}]]");
        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(documento.RootElement);

        lido.Errors.Select(static erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(
            ("[0][0].valor", CodigoDaForma, MensagemDaForma), ("[0][1].valor", CodigoDaForma, MensagemDaForma));
        lido.Errors.Should().OnlyContain(erro => !erro.Field!.Contains("texto fictício", StringComparison.Ordinal)
            && !erro.Error.Message.Contains("texto fictício", StringComparison.Ordinal));
    }

    [Fact]
    public void DeJson_ClausulaVaziaECondicaoSemOperador_AcumulaNaOrdem()
    {
        using JsonDocument documento = JsonDocument.Parse("""[[],[{"codigoDoDado":"DADO_FICTICIO","valor":true}]]""");
        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(documento.RootElement);

        lido.Errors.Select(static erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(
            ("[0]", PredicadoDnfJson.FormaInvalida, "Cada cláusula é uma lista não vazia de condições."),
            ("[1][0].operador", PredicadoDnfJson.FormaInvalida, "Cada condição tem codigoDoDado, operador e valor."));
    }

    [Fact]
    public void DeJson_DuasClausulasInvalidas_AcumulaNaOrdem()
    {
        using JsonDocument documento = JsonDocument.Parse("[1,[]]");
        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(documento.RootElement);

        lido.Errors.Select(static erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(
            ("[0]", PredicadoDnfJson.FormaInvalida, "Cada cláusula é uma lista não vazia de condições."),
            ("[1]", PredicadoDnfJson.FormaInvalida, "Cada cláusula é uma lista não vazia de condições."));
    }

    [Theory]
    [InlineData("{}", "", "O predicado é uma lista de cláusulas.")]
    [InlineData("[[]]", "[0]", "Cada cláusula é uma lista não vazia de condições.")]
    [InlineData("[[1]]", "[0][0]", "Cada condição tem codigoDoDado, operador e valor.")]
    [InlineData("""[[{"codigoDoDado":"DADO","operador":"IGUAL"}]]""", "[0][0].valor", "Cada condição tem codigoDoDado, operador e valor.")]
    public void DeJson_DefeitoIsolado_PreservaRecusa(string json, string campo, string mensagem)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(documento.RootElement);

        lido.Errors.Select(static erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(
            (campo, PredicadoDnfJson.FormaInvalida, mensagem));
    }

    [Fact]
    public void DeJson_FormaIncoerenteIsolada_PreservaRecusa()
    {
        using JsonDocument documento = JsonDocument.Parse($"[[{CondicaoInvalida}]]");
        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(documento.RootElement);

        lido.Errors.Select(static erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(
            ("[0][0].valor", CodigoDaForma, MensagemDaForma));
    }

    [Fact]
    public void ObrigatoriedadeDeJson_DuasCausasDistintas_GuardaPrimeiraCausa()
    {
        using JsonDocument documento = JsonDocument.Parse("""{"tipo":"QUANDO","predicado":[[],[{"codigoDoDado":"DADO","valor":true}]]}""");
        Result<Obrigatoriedade> lida = PredicadoDnfJson.ObrigatoriedadeDeJson(documento.RootElement);

        lida.Error.Should().Be(new DomainError(PredicadoDnfJson.FormaInvalida, "Cada cláusula é uma lista não vazia de condições."));
    }
}
