namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

public sealed class LeituraDosConversoresDeRegrasTests
{
    [Fact(DisplayName = "Restrição inválida informa o código sem o conteúdo")]
    public void LerRestricoes_FormaInvalida_InformaCodigo()
    {
        const string json = """[{"tipo":"FAIXA_NUMERICA","minimo":"valor-gravado-ficticio-7f3a"}]""";
        string mensagem = FluentActions.Invoking(() => ConversoresDeRegras.Restricoes.ConvertFromProvider(json))
            .Should().Throw<InvalidOperationException>().Which.Message;
        mensagem.Should().Be("Dado inválido no banco ao reidratar RestricaoValor: RestricaoValor.FormaJsonInvalida. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        mensagem.Should().NotContain("valor-gravado-ficticio-7f3a");
        mensagem.Should().NotContain("faixa numérica");
    }

    [Fact(DisplayName = "JSON inválido não leva conteúdo nem causa")]
    public void LerRestricoes_JsonInvalido_OcultaConteudoECausa()
    {
        const string json = "[valor-gravado-ficticio-7f3a";
        InvalidOperationException erro = FluentActions.Invoking(() => ConversoresDeRegras.Restricoes.ConvertFromProvider(json))
            .Should().Throw<InvalidOperationException>().Which;
        erro.Message.Should().Be("Dado inválido no banco ao reidratar RestricaoValor: documento JSON inválido. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        erro.Message.Should().NotContain("valor-gravado-ficticio-7f3a");
        erro.InnerException.Should().BeNull();
    }

    [Fact(DisplayName = "Impedimento nulo continua ausente")]
    public void LerImpedimento_Nulo_DevolveAusencia() =>
        ConversoresDeRegras.Impedimento.ConvertFromProvider("null").Should().BeNull();
    [Theory(DisplayName = "As regras gravadas informam todos os códigos do predicado na ordem")]
    [InlineData("obrigatoriedade")]
    [InlineData("impedimento")]
    [InlineData("restricoes")]
    public void LerRegra_PredicadoComRecusas_InformaCodigosSemDuplicata(string tipo)
    {
        const string predicado = """[[{"codigoDoDado":" ","operador":"IGUAL","valor":true},{"codigoDoDado":"DADO","operador":"INVALIDO","valor":true},{"codigoDoDado":" ","operador":"IGUAL","valor":true}]]""";
        string json = tipo switch
        {
            "obrigatoriedade" => $$"""{"tipo":"QUANDO","predicado":{{predicado}}}""",
            "impedimento" => $$"""{"quando":{{predicado}},"mensagem":"Mensagem"}""",
            _ => $$"""[{"tipo":"OPCOES_PERMITIDAS","entradas":[{"quando":{{predicado}},"valores":["A"]}]}]""",
        };
        Action leitura = () => LerRegra(tipo, json);
        leitura.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(
            "CondicaoDnf.FatoObrigatorio, CondicaoDnf.OperadorInvalido.");
    }

    private static object? LerRegra(string tipo, string json) => tipo switch
    {
        "obrigatoriedade" => ConversoresDeRegras.Obrigatoriedade.ConvertFromProvider(json),
        "impedimento" => ConversoresDeRegras.Impedimento.ConvertFromProvider(json),
        _ => ConversoresDeRegras.Restricoes.ConvertFromProvider(json),
    };

}
