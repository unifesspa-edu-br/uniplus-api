namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;

/// <summary>
/// O acréscimo de valor ao fato categórico do administrador: a ordem é exigida, e zero dito é aceito e
/// devolvido.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OrdemDoValorDoFatoEndpointTests
{
    private const string Fatos = "/api/configuracao/admin/fatos-candidato";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public OrdemDoValorDoFatoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O valor sem ordem é recusado no campo ordem e nada é gravado no fato")]
    public async Task AcrescentarValor_SemOrdem_RecusaENaoGrava() =>
        await ConferirRecusaAsync(new { codigo = "REGULAR", descricao = "Ensino regular" });

    [Fact(DisplayName = "O valor com ordem nula é recusado no campo ordem e nada é gravado no fato")]
    public async Task AcrescentarValor_OrdemNula_RecusaENaoGrava() =>
        await ConferirRecusaAsync(new { codigo = "REGULAR", descricao = "Ensino regular", ordem = (int?)null });

    [Fact(DisplayName = "O valor com ordem zero é aceito e a ordem zero é devolvida")]
    public async Task AcrescentarValor_OrdemZero_GravaEDevolveZero()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarFatoAsync(client);

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{id}/valores", new { codigo = "REGULAR", descricao = "Ensino regular", ordem = 0 });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        JsonElement valor = (await ValoresDeclaradosAsync(client, id)).Should().ContainSingle().Subject;
        (valor.GetProperty("codigo").GetString(), valor.GetProperty("ordem").GetInt32()).Should().Be(("REGULAR", 0));
    }

    private async Task ConferirRecusaAsync(object corpo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarFatoAsync(client);

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{id}/valores", corpo);

        string detalhe = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, detalhe);
        using JsonDocument problema = JsonDocument.Parse(detalhe);
        JsonElement erro = problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle().Subject;
        (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString()).Should().Be(("ordem", "uniplus.validacao.obrigatorio"));
        (await ValoresDeclaradosAsync(client, id)).Should().BeEmpty();
    }

    private static async Task<Guid> CadastrarFatoAsync(HttpClient client)
    {
        HttpResponseMessage criado = await EnviarAsync(client, HttpMethod.Post, Fatos, FatoCategorico());
        criado.StatusCode.Should().Be(HttpStatusCode.Created, await criado.Content.ReadAsStringAsync());
        return await criado.Content.ReadFromJsonAsync<Guid>();
    }

    private static object FatoCategorico() => new
    {
        codigo = CodigoDe("FATO", 12),
        nome = "Forma de conclusão",
        descricao = "Descrição do fato.",
        dominio = "OPCAO",
        cardinalidade = "VALOR_UNICO",
        fonteValores = "CATALOGO",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    private static async Task<JsonElement[]> ValoresDeclaradosAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Fatos}/{id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument envelope = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. envelope.RootElement.GetProperty("fato").GetProperty("definicao").GetProperty("valores").GetProperty("declarados")
            .EnumerateArray().Select(static v => v.Clone())];
    }
}
