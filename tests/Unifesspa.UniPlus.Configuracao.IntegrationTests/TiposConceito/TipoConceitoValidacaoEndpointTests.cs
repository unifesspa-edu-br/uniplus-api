namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposConceito;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure.TiposConceitoHttpDeTeste;

/// <summary>
/// A definição inválida do tipo de conceito volta como 422 com todas as violações no mesmo lote, cada uma no
/// campo em camelCase, e a validação vence o 404 na edição.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoConceitoValidacaoEndpointTests
{
    private const string Prefixo = "uniplus.configuracao.tipo_conceito.";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoConceitoValidacaoEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "POST de numérico sem escala e sem nome devolve as duas violações em errors[]")]
    public async Task Criar_NumericoSemEscalaESemNome_AcumulaAsViolacoes()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostarAsync(client, Numerico().Sem("numerico").Com("nome", ""));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Equal(
            ("nome", $"{Prefixo}nome_obrigatorio"),
            ("numerico", $"{Prefixo}escala_numerica_obrigatoria"));
    }

    [Fact(DisplayName = "POST de categórico com um só valor e sem nome devolve as duas violações em errors[]")]
    public async Task Criar_CategoricoComUmValorESemNome_AcumulaAsViolacoes()
    {
        object umValor = new object[] { new { codigo = "DEFERIDO", rotulo = "Deferido", contaComoAprovacao = true } };
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostarAsync(client, Categorico().Com("valores", umValor).Com("nome", ""));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Equal(
            ("nome", $"{Prefixo}nome_obrigatorio"),
            ("valores", $"{Prefixo}valores_categoricos_insuficientes"));
    }

    [Fact(DisplayName = "POST de categórico sem valores retorna 422 valores_categoricos_insuficientes")]
    public async Task Criar_CategoricoSemValores_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostarAsync(client, Categorico().Sem("valores"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Equal(("valores", $"{Prefixo}valores_categoricos_insuficientes"));
    }

    [Fact(DisplayName = "POST de categórico com valor sem o campo contaComoAprovacao retorna 422 no campo do valor")]
    public async Task Criar_ValorSemContaComoAprovacao_Retorna422()
    {
        object[] valores = [new { codigo = "DEFERIDO", rotulo = "Deferido", contaComoAprovacao = true }, new { codigo = "INDEFERIDO", rotulo = "Indeferido" }];
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostarAsync(client, Categorico().Com("valores", valores));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Equal(("valores[1].contaComoAprovacao", $"{Prefixo}valor_categorico_conta_como_aprovacao_obrigatoria"));
    }

    [Theory(DisplayName = "POST com definição incoerente retorna 422 no campo da violação")]
    [InlineData("natureza_desconhecida", "natureza", "natureza_invalida")]
    [InlineData("escala_invertida", "numerico", "escala_numerica_invalida")]
    [InlineData("casas_demais", "numerico.casasDecimais", "casas_decimais_invalidas")]
    [InlineData("numerico_com_valores", "valores", "parametros_incompativeis_com_a_natureza")]
    [InlineData("categorico_com_escala", "numerico", "parametros_incompativeis_com_a_natureza")]
    [InlineData("codigo_de_valor_minusculo", "valores[0].codigo", "valor_categorico_codigo_invalido")]
    [InlineData("codigo_de_valor_repetido", "valores[1].codigo", "valor_categorico_duplicado")]
    public async Task Criar_DefinicaoIncoerente_Retorna422(string caso, string campo, string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostarAsync(client, CorpoIncoerente(caso));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Contain((campo, $"{Prefixo}{codigo}"));
    }

    [Fact(DisplayName = "PUT inexistente com corpo inválido retorna 422: a validação vence o 404")]
    public async Task Atualizar_InexistenteComCorpoInvalido_Retorna422()
    {
        Guid id = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await AtualizarAsync(
            client, id, Numerico().Sem("codigo").Com("id", id).Sem("numerico"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Equal(("numerico", $"{Prefixo}escala_numerica_obrigatoria"));
    }

    [Fact(DisplayName = "PUT inválido não altera o tipo gravado")]
    public async Task Atualizar_CorpoInvalido_MantemOTipo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Numerico());

        using HttpResponseMessage resposta = await AtualizarAsync(
            client, id, Numerico().Sem("codigo").Com("id", id).Com("nome", "Nome que não deve gravar").Sem("numerico"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument tipo = await LerAsync(await ObterAsync(client, id));
        tipo.RootElement.GetProperty("nome").GetString().Should().Be("Nota da entrevista");
    }

    private static Dictionary<string, object?> CorpoIncoerente(string caso) => caso switch
    {
        "natureza_desconhecida" => Numerico().Com("natureza", "textual"),
        "escala_invertida" => Numerico().Com("numerico", new { escalaMinima = 10m, escalaMaxima = 0m, casasDecimais = 2 }),
        "casas_demais" => Numerico().Com("numerico", new { escalaMinima = 0m, escalaMaxima = 10m, casasDecimais = 5 }),
        "numerico_com_valores" => Numerico().Com("valores", Categorico()["valores"]),
        "categorico_com_escala" => Categorico().Com("numerico", Numerico()["numerico"]),
        "codigo_de_valor_minusculo" => Categorico().Com("valores", ValoresCom("deferido", "INDEFERIDO")),
        "codigo_de_valor_repetido" => Categorico().Com("valores", ValoresCom("DEFERIDO", "DEFERIDO")),
        _ => throw new InvalidOperationException($"Caso de teste inesperado: {caso}"),
    };

    private static object[] ValoresCom(string primeiro, string segundo) =>
    [
        new { codigo = primeiro, rotulo = "Primeiro valor", contaComoAprovacao = true },
        new { codigo = segundo, rotulo = "Segundo valor", contaComoAprovacao = false },
    ];

    private static async Task<IReadOnlyList<(string? Campo, string? Codigo)>> ErrosAsync(HttpResponseMessage resposta)
    {
        using JsonDocument problema = await LerAsync(resposta);
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(erro => (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString()))];
    }
}
