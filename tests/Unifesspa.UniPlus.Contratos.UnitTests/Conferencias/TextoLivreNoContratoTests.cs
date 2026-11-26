namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.OpenApi;

using TestSupport;

/// <summary>Como a conferência dos parâmetros de busca decide que um parâmetro do contrato é texto livre.</summary>
public sealed class TextoLivreNoContratoTests
{
    [Fact(DisplayName = "String sem enum nem formato é texto livre")]
    public void StringSemEnumNemFormato_ETextoLivre() =>
        TextoLivreNoContrato.Eh(new OpenApiSchema { Type = JsonSchemaType.String }).Should().BeTrue();

    [Fact(DisplayName = "Referência a esquema string sem enum é texto livre")]
    public void ReferenciaAStringSemEnum_ETextoLivre() =>
        TextoLivreNoContrato.Eh(Referencia(new OpenApiSchema { Type = JsonSchemaType.String })).Should().BeTrue();

    [Fact(DisplayName = "Referência a enumeração de códigos é valor controlado")]
    public void ReferenciaAEnum_EValorControlado() =>
        TextoLivreNoContrato.Eh(Referencia(new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create("ATIVA")!, JsonValue.Create("VENCIDA")!] }))
            .Should().BeFalse();

    [Fact(DisplayName = "Referência que não se resolve é texto livre")]
    public void ReferenciaNaoResolvida_ETextoLivre() =>
        TextoLivreNoContrato.Eh(new OpenApiSchemaReference("Inexistente", new OpenApiDocument())).Should().BeTrue();

    [Theory(DisplayName = "Formato que pode carregar dado pessoal, ou desconhecido, é texto livre")]
    [InlineData("email")]
    [InlineData("uri")]
    [InlineData("hostname")]
    [InlineData("formato-desconhecido")]
    public void FormatoComPossivelDadoPessoal_ETextoLivre(string formato) =>
        TextoLivreNoContrato.Eh(new OpenApiSchema { Type = JsonSchemaType.String, Format = formato }).Should().BeTrue();

    [Theory(DisplayName = "Formato sem dado pessoal é valor controlado")]
    [InlineData("uuid")]
    [InlineData("date")]
    [InlineData("date-time")]
    public void FormatoSemDadoPessoal_EValorControlado(string formato) =>
        TextoLivreNoContrato.Eh(new OpenApiSchema { Type = JsonSchemaType.String, Format = formato }).Should().BeFalse();

    [Fact(DisplayName = "Lista de strings livres é texto livre, e lista de números não")]
    public void Lista_SegueOTipoDosItens()
    {
        OpenApiSchema deTexto = new() { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } };
        OpenApiSchema deNumeros = new() { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" } };

        (TextoLivreNoContrato.Eh(deTexto), TextoLivreNoContrato.Eh(deNumeros)).Should().Be((true, false));
    }

    [Theory(DisplayName = "Composição com alguma ramificação de string livre é texto livre")]
    [InlineData("allOf")]
    [InlineData("oneOf")]
    [InlineData("anyOf")]
    public void ComposicaoComStringLivre_ETextoLivre(string composicao) =>
        TextoLivreNoContrato.Eh(Composto(composicao, Livre(), Enumeracao())).Should().BeTrue();

    [Theory(DisplayName = "Composição só de enumeração, formato seguro ou nulo é valor controlado")]
    [InlineData("allOf")]
    [InlineData("oneOf")]
    [InlineData("anyOf")]
    public void ComposicaoControlada_EValorControlado(string composicao) =>
        TextoLivreNoContrato.Eh(Composto(composicao, Enumeracao(), new OpenApiSchema { Type = JsonSchemaType.Null })).Should().BeFalse();

    [Fact(DisplayName = "Esquema sem tipo, sem enum e sem composição é texto livre, e com enum não")]
    public void EsquemaSemTipo_ReprovaNaDuvida()
    {
        (TextoLivreNoContrato.Eh(new OpenApiSchema()), TextoLivreNoContrato.Eh(new OpenApiSchema { Enum = [JsonValue.Create("ATIVA")!] }))
            .Should().Be((true, false));
    }

    [Fact(DisplayName = "Parâmetro de consulta declarado no caminho é detectado")]
    public void ParametroNoCaminho_EDetectado()
    {
        OpenApiPathItem caminho = new() { Parameters = [Consulta("q", Livre())] };

        ParametrosDeConsultaPublicados.De(caminho, new OpenApiOperation()).Should().Equal(new ParametroDeConsulta("q", true));
    }

    [Fact(DisplayName = "O parâmetro da operação substitui o do caminho com o mesmo nome e local")]
    public void ParametroDaOperacao_SubstituiODoCaminho()
    {
        OpenApiPathItem caminho = new() { Parameters = [Consulta("q", Livre())] };
        OpenApiOperation operacao = new() { Parameters = [Consulta("q", new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" })] };

        ParametrosDeConsultaPublicados.De(caminho, operacao).Should().Equal(new ParametroDeConsulta("q", false));
    }

    [Fact(DisplayName = "Parâmetro de outro local com o mesmo nome não substitui o de consulta do caminho")]
    public void ParametroDeOutroLocal_NaoSubstitui()
    {
        OpenApiPathItem caminho = new() { Parameters = [Consulta("q", Livre())] };
        OpenApiOperation operacao = new() { Parameters = [new OpenApiParameter { Name = "q", In = ParameterLocation.Header, Schema = Livre() }] };

        ParametrosDeConsultaPublicados.De(caminho, operacao).Should().Equal(new ParametroDeConsulta("q", true));
    }

    private static OpenApiSchema Livre() => new() { Type = JsonSchemaType.String };

    private static OpenApiSchema Enumeracao() => new() { Type = JsonSchemaType.String, Enum = [JsonValue.Create("ATIVA")!] };

    private static OpenApiParameter Consulta(string nome, OpenApiSchema esquema) =>
        new() { Name = nome, In = ParameterLocation.Query, Schema = esquema };

    private static OpenApiSchema Composto(string composicao, params OpenApiSchema[] ramos)
    {
        OpenApiSchema esquema = new();
        IList<IOpenApiSchema> destino = composicao switch
        {
            "allOf" => esquema.AllOf = [],
            "oneOf" => esquema.OneOf = [],
            _ => esquema.AnyOf = [],
        };
        foreach (OpenApiSchema ramo in ramos)
        {
            destino.Add(ramo);
        }

        return esquema;
    }

    private static OpenApiSchemaReference Referencia(OpenApiSchema alvo)
    {
        OpenApiDocument documento = new() { Components = new OpenApiComponents { Schemas = new Dictionary<string, IOpenApiSchema> { ["Alvo"] = alvo } } };
        documento.Workspace?.RegisterComponents(documento);
        return new OpenApiSchemaReference("Alvo", documento);
    }
}
