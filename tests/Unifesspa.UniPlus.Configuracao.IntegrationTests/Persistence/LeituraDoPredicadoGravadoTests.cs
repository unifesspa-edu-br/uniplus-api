namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

public sealed class LeituraDoPredicadoGravadoTests
{
    [Fact(DisplayName = "Predicado incoerente informa o código sem o conteúdo")]
    public void LerPredicado_ValorIncoerente_InformaCodigo()
    {
        const string json = """[[{"codigoDoDado":"VALOR_FICTICIO_7F3A","operador":"IGUAL","valor":[]}]]""";
        string mensagem = FluentActions.Invoking(() => PredicadoDnfColunaJson.Desserializar(json))
            .Should().Throw<InvalidOperationException>().Which.Message;
        mensagem.Should().Be("Dado inválido no banco ao reidratar PredicadoDnf: CondicaoDnf.FormaIncoerenteComOperador. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        mensagem.Should().NotContain("VALOR_FICTICIO_7F3A").And.NotContain("exigem");
    }

    [Fact(DisplayName = "Predicado gravado com duas condições inválidas informa os dois códigos, na ordem")]
    public void LerPredicado_DuasCondicoesInvalidas_InformaTodosOsCodigos()
    {
        const string json = """[[{"codigoDoDado":" ","operador":"IGUAL","valor":true}],[{"codigoDoDado":"DADO","operador":"INVALIDO","valor":true}]]""";

        Action leitura = () => PredicadoDnfColunaJson.Desserializar(json);

        leitura.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(
            "CondicaoDnf.FatoObrigatorio, CondicaoDnf.OperadorInvalido.");
    }

    [Fact(DisplayName = "Regra sem contribuição informa o código sem o conteúdo")]
    public void LerRegras_ContribuicaoVazia_InformaCodigo()
    {
        const string json = """[{"contribui":"","quando":[[{"codigoDoDado":"VALOR_FICTICIO_7F3A","operador":"IGUAL","valor":true}]]}]""";
        string mensagem = FluentActions.Invoking(() => RegrasPadraoJson.Desserializar(json))
            .Should().Throw<InvalidOperationException>().Which.Message;
        mensagem.Should().Be("Dado inválido no banco ao reidratar RegraDerivacao: RegraDerivacao.ContribuiObrigatorio. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        mensagem.Should().NotContain("VALOR_FICTICIO_7F3A").And.NotContain("contribuir");
    }

    [Fact(DisplayName = "O JSON null na coluna do predicado falha na leitura, sem virar lista vazia")]
    public void LerPredicado_JsonNulo_Falha() =>
        ConferirJsonNulo(() => PredicadoDnfColunaJson.Desserializar("null"), "PredicadoDnf");

    [Fact(DisplayName = "O JSON null na coluna das regras padrão falha na leitura, sem virar lista vazia")]
    public void LerRegras_JsonNulo_Falha() =>
        ConferirJsonNulo(() => RegrasPadraoJson.Desserializar("null"), "RegraDerivacao");

    private static void ConferirJsonNulo(Action leitura, string nome) =>
        leitura.Should().Throw<InvalidOperationException>().Which.Message.Should().Be(
            $"Dado inválido no banco ao reidratar {nome}: documento JSON nulo. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");

    [Fact(DisplayName = "A lista vazia gravada como lista vazia continua valendo")]
    public void Ler_ListaVazia_Vale()
    {
        PredicadoDnfColunaJson.Desserializar("[]").Should().NotBeNull();
        RegrasPadraoJson.Desserializar("[]").Should().BeEmpty();
    }

    [Theory(DisplayName = "JSON inválido não leva conteúdo nem causa")]
    [InlineData(true, "PredicadoDnf")]
    [InlineData(false, "RegraDerivacao")]
    public void Ler_JsonInvalido_OcultaConteudoECausa(bool predicado, string nome)
    {
        const string json = "[valor-gravado-ficticio-7f3a";
        Action leitura = () => { if (predicado) { PredicadoDnfColunaJson.Desserializar(json); } else { RegrasPadraoJson.Desserializar(json); } };
        InvalidOperationException erro = leitura.Should().Throw<InvalidOperationException>().Which;
        erro.Message.Should().Be($"Dado inválido no banco ao reidratar {nome}: documento JSON inválido. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        erro.Message.Should().NotContain("valor-gravado-ficticio-7f3a");
        erro.InnerException.Should().BeNull();
    }

    [Theory(DisplayName = "O conteúdo do modelo informa todos os códigos do predicado na ordem")]
    [InlineData("obrigatoriedade")]
    [InlineData("impedimento")]
    [InlineData("restricoes")]
    public void LerConteudo_PredicadoComRecusas_InformaCodigosSemDuplicata(string tipo)
    {
        const string predicado = """[[{"codigoDoDado":" ","operador":"IGUAL","valor":true},{"codigoDoDado":"DADO","operador":"INVALIDO","valor":true},{"codigoDoDado":" ","operador":"IGUAL","valor":true}]]""";
        string regras = tipo switch
        {
            "obrigatoriedade" => $$""","obrigatoriedade":{"tipo":"QUANDO","predicado":{{predicado}}},"restricoes":[],"impedimento":null""",
            "impedimento" => $$""","obrigatoriedade":{"tipo":"SEMPRE"},"restricoes":[],"impedimento":{"quando":{{predicado}},"mensagem":"Mensagem"}""",
            _ => $$""","obrigatoriedade":{"tipo":"SEMPRE"},"restricoes":[{"tipo":"OPCOES_PERMITIDAS","entradas":[{"quando":{{predicado}},"valores":["A"]}]}],"impedimento":null""",
        };
        string json = $$"""{"titulo":null,"paginas":[],"itens":[{"codigoDoDado":"DADO","ordem":0,"paginaCodigo":null,"rotulo":"Pergunta","tipoDeCampo":"SIM_OU_NAO","pedirConfirmacao":false{{regras}}}],"termos":[],"pressupostos":[],"grupos":[]}""";
        Action leitura = () => ConteudoDoModeloJson.Desserializar(json);
        leitura.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(
            "CondicaoDnf.FatoObrigatorio, CondicaoDnf.OperadorInvalido.");
    }
}
