namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// As definições compartilhadas do esquema da configuração: a referência a uma regra do catálogo, o predicado em forma
/// normal disjuntiva e a obrigatoriedade, que várias partes do documento reutilizam.
/// </summary>
public sealed class DefinicoesComunsDoEsquemaDaConfiguracaoTests
{
    private const string Comuns = "comuns.schema.json";
    private const string Condicoes = "condicoes.schema.json";

    [Fact(DisplayName = "A referência de regra tem código e versão e recusa propriedade desconhecida")]
    public void ReferenciaDeRegra_TemCodigoEVersao()
    {
        CaminhosComErro(Documento("""{ "codigo": "PRAZO-DIAS-UTEIS", "versao": "v1" }"""), $"{Comuns}#/$defs/referenciaDeRegra").Should().BeEmpty();
        CaminhosComErro(Documento("""{ "codigo": "PRAZO-DIAS-UTEIS", "id": "x" }"""), $"{Comuns}#/$defs/referenciaDeRegra").Should().Contain("/id");
    }

    [Theory(DisplayName = "A versão da regra só tem a forma do catálogo, vN")]
    [InlineData("1")]
    [InlineData("1.0.0")]
    [InlineData("V1")]
    [InlineData("v")]
    public void ReferenciaDeRegra_VersaoForaDaFormaDoCatalogo_Recusa(string versao) =>
        CaminhosComErro(Documento($$"""{ "codigo": "PRAZO-DIAS-UTEIS", "versao": "{{versao}}" }"""), $"{Comuns}#/$defs/referenciaDeRegra")
            .Should().Contain("/versao");

    [Fact(DisplayName = "O predicado sem cláusula e o de uma cláusula com duas condições validam")]
    public void Predicado_ClausulasEmOuCondicoesEmE_Valida()
    {
        JsonNode semClausula = Documento("[]");
        JsonNode umaClausula = Documento("""
            [ [ { "codigoDoDado": "RENDA_PER_CAPITA", "operador": "MENOR_IGUAL", "valor": 1.5 },
                { "codigoDoDado": "UF", "operador": "ALGUM_DE", "valor": ["PA", "TO"] } ] ]
            """);

        CaminhosComErro(semClausula, $"{Condicoes}#/$defs/condicoesAlternativas").Should().BeEmpty();
        CaminhosComErro(umaClausula, $"{Condicoes}#/$defs/condicoesAlternativas").Should().BeEmpty();
    }

    [Fact(DisplayName = "A cláusula vazia é recusada com o caminho dela")]
    public void Predicado_ClausulaVazia_Recusa() =>
        CaminhosComErro(Documento("[ [] ]"), $"{Condicoes}#/$defs/condicoesAlternativas").Should().Contain("/0");

    [Theory(DisplayName = "O valor incoerente com o operador é recusado")]
    [InlineData("ALGUM_DE", "\"PA\"")]
    [InlineData("NENHUM_DE", "1")]
    [InlineData("IGUAL", "[\"PA\"]")]
    [InlineData("MAIOR_IGUAL", "{ \"a\": 1 }")]
    public void Condicao_ValorIncoerenteComOOperador_Recusa(string operador, string valor)
    {
        ArgumentNullException.ThrowIfNull(operador);
        ArgumentNullException.ThrowIfNull(valor);
        JsonNode condicao = Documento($$"""{ "codigoDoDado": "UF", "operador": "{{operador}}", "valor": {{valor}} }""");

        CaminhosComErro(condicao, $"{Condicoes}#/$defs/condicao").Should().Contain("/valor");
    }

    [Fact(DisplayName = "O operador desconhecido é recusado")]
    public void Condicao_OperadorDesconhecido_Recusa() =>
        CaminhosComErro(Documento("""{ "codigoDoDado": "UF", "operador": "PARECIDO", "valor": "PA" }"""), $"{Condicoes}#/$defs/condicao")
            .Should().Contain("/operador");

    [Fact(DisplayName = "A obrigatoriedade QUANDO exige o predicado e as outras o recusam")]
    public void Obrigatoriedade_PredicadoSoEmQuando()
    {
        string definicao = $"{Condicoes}#/$defs/obrigatoriedade";

        CaminhosComErro(Documento("""{ "tipo": "SEMPRE" }"""), definicao).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "tipo": "NUNCA", "quando": null }"""), definicao).Should().BeEmpty();
        CaminhosComErro(Documento("""{ "tipo": "QUANDO", "quando": [ [ { "codigoDoDado": "UF", "operador": "IGUAL", "valor": "PA" } ] ] }"""), definicao)
            .Should().BeEmpty();
        CaminhosComErro(Documento("""{ "tipo": "QUANDO" }"""), definicao).Should().NotBeEmpty();
        CaminhosComErro(Documento("""{ "tipo": "SEMPRE", "quando": [] }"""), definicao).Should().Contain("/quando");
    }

    [Fact(DisplayName = "A obrigatoriedade QUANDO com predicado nulo é recusada")]
    public void Obrigatoriedade_QuandoComPredicadoNulo_Recusa() =>
        CaminhosComErro(Documento("""{ "tipo": "QUANDO", "quando": null }"""), $"{Condicoes}#/$defs/obrigatoriedade").Should().Contain("/quando");

    [Theory(DisplayName = "O fato ou o valor em branco da condição é recusado")]
    [InlineData("\"  \"", "\"PA\"", "/codigoDoDado")]
    [InlineData("\"UF\"", "\"  \"", "/valor")]
    public void Condicao_TextoEmBranco_Recusa(string fato, string valor, string caminho)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(valor);
        JsonNode condicao = Documento($$"""{ "codigoDoDado": {{fato}}, "operador": "IGUAL", "valor": {{valor}} }""");

        CaminhosComErro(condicao, $"{Condicoes}#/$defs/condicao").Should().Contain(caminho);
    }

    [Fact(DisplayName = "O item de texto em branco na lista de ALGUM_DE é recusado e a lista vazia é aceita")]
    public void Condicao_ListaComItemEmBranco_Recusa()
    {
        string definicao = $"{Condicoes}#/$defs/condicao";

        CaminhosComErro(Documento("""{ "codigoDoDado": "UF", "operador": "ALGUM_DE", "valor": ["PA", " "] }"""), definicao).Should().Contain("/valor/1");
        CaminhosComErro(Documento("""{ "codigoDoDado": "UF", "operador": "ALGUM_DE", "valor": [] }"""), definicao).Should().BeEmpty();
    }
}
