namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
/// <summary>
/// O fato de sistema e o fato semeado no circuito da proteção do catálogo e o detalhe do item que o Representante do
/// controlador lê para decidir, pelo host real (ADR-0153).
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class DetalheDaProtecaoDoCatalogoEndpointTests : ProtecaoDoCatalogoTestBase
{
    public DetalheDaProtecaoDoCatalogoEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "No fato de sistema a hipótese legal se edita e o deixa pendente; o domínio continua travado")]
    public async Task FatoDeSistema_ProtecaoEditavelEDefinicaoTravada()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        JsonElement original = await ObterFatoAsync(client, IdDeQuilombola);

        HttpResponseMessage travada = await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original, dominio: "TEXTO"));
        travada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await travada.Content.ReadAsStringAsync()).Should().Contain("definicao_do_fato_de_sistema_travada");

        HttpResponseMessage editada = await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original, hipoteseLegal: "EXERCICIO_REGULAR_DE_DIREITOS"));
        editada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement depois = await ObterFatoAsync(client, IdDeQuilombola);
        (depois.GetProperty("hipoteseLegal").GetString(), depois.GetProperty("aprovacao").GetProperty("situacao").GetString()).Should().Be(("EXERCICIO_REGULAR_DE_DIREITOS", "PENDENTE"));

        (await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "O aceite da recomendação aprova o fato semeado com a classificação indicada, sem mudar o código")]
    public async Task FatoSemeado_AceitaARecomendacao()
    {
        await TornarCarolineRepresentanteAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        JsonElement original = await ObterFatoAsync(client, IdDeQuilombola);
        (await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SolicitarAsync(client, "FATO", IdDeQuilombola)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await DecidirAsync(client, "FATO", IdDeQuilombola, "RECOMENDAR_AJUSTE", "PESSOAL")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{IdDeQuilombola}/aceite-da-recomendacao")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement aceito = await ObterFatoAsync(client, IdDeQuilombola);
        (aceito.GetProperty("codigo").GetString(), aceito.GetProperty("classificacaoProtecao").GetString(), aceito.GetProperty("aprovacao").GetProperty("situacao").GetString())
            .Should().Be(("QUILOMBOLA", "PESSOAL", "APROVADA"));
        (await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "O detalhe do fato traz o que ele é, no tipo de mídia do detalhe")]
    public async Task Detalhe_DoFato_OQueEle()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await FatoComValoresAsync(client);

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/fatos/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.protecao-do-catalogo-detalhe.v1+json");
        JsonElement detalhe = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        (detalhe.GetProperty("tipo").GetString(), detalhe.GetProperty("nome").GetString(), detalhe.GetProperty("descricao").GetString())
            .Should().Be(("FATO", "Identidade de gênero", "Como a pessoa se identifica."));
        (detalhe.GetProperty("classificacaoProtecao").GetString(), detalhe.GetProperty("aprovacao").GetProperty("situacao").GetString()).Should().Be(("SENSIVEL", "PENDENTE"));
        detalhe.GetProperty("tipoDocumento").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "O detalhe do fato traz o domínio e só os valores ativos, na ordem")]
    public async Task Detalhe_DoFato_ValoresAtivosNaOrdem()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await FatoComValoresAsync(client);

        JsonElement fato = (await (await EnviarAsync(client, HttpMethod.Get, $"{Base}/fatos/{id}")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fato");

        (fato.GetProperty("dominio").GetString(), fato.GetProperty("cardinalidade").GetString(), fato.GetProperty("sistema").GetBoolean()).Should().Be(("CATEGORICO", "ESCALAR", false));
        fato.GetProperty("valores").EnumerateArray().Select(v => v.GetProperty("codigo").GetString()).Should().Equal("MULHER", "OUTRA");
        fato.GetProperty("valores")[0].GetProperty("orientacao").GetString().Should().Be("Escolha conforme se identifica.");
    }

    [Fact(DisplayName = "O detalhe do tipo de documento traz o que é pedido ao candidato")]
    public async Task Detalhe_DoTipoDeDocumento()
    {
        await TornarCarolineRepresentanteAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, Tipos, NovoTipoDetalhado());

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/tipos-documento/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement detalhe = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        (detalhe.GetProperty("tipo").GetString(), detalhe.GetProperty("descricao").GetString(), detalhe.GetProperty("aprovacao").GetProperty("situacao").GetString())
            .Should().Be(("TIPO_DOCUMENTO", "Laudo que atesta a condição de saúde.", "PENDENTE"));
        detalhe.GetProperty("fato").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement tipo = detalhe.GetProperty("tipoDocumento");
        (tipo.GetProperty("categoria").GetString(), tipo.GetProperty("formatosAceitos").GetString(), tipo.GetProperty("tamanhoMaximoMb").GetInt32()).Should().Be(("SAUDE", "PDF", 5));
    }

    [Theory(DisplayName = "O detalhe de item inexistente, ou de cadastro que não é da fila, responde 404")]
    [InlineData("tipos-documento")]
    [InlineData("fatos")]
    [InlineData("outro-cadastro")]
    public async Task Detalhe_Inexistente_Retorna404(string cadastro)
    {
        await TornarCarolineRepresentanteAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Get, $"{Base}/{cadastro}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Quem mantém os cadastros, mas não é Representante do controlador, recebe 403 no detalhe")]
    public async Task Detalhe_SemPermissaoDeAprovar_Retorna403()
    {
        await TornarCarolineRepresentanteAsync();
        await TornarPauloMantenedorAsync(
            PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo, PermissaoConfiguracao.FatosCandidatoConsultar.Codigo);
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "INTERNO");

        HttpResponseMessage resposta = await EnviarComoPauloAsync(client, HttpMethod.Get, $"{Base}/tipos-documento/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> CadastrarAsync(HttpClient client, string rota, object corpo)
    {
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, rota, corpo);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    // O fato sensível com os valores MULHER e OUTRA ativos e o valor ANTIGA excluído.
    private async Task<Guid> FatoComValoresAsync(HttpClient client)
    {
        await TornarCarolineRepresentanteAsync();
        Guid id = await CadastrarAsync(client, Fatos, NovoFatoSensivel());
        (await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{id}/valores", new { codigo = "OUTRA", descricao = "Outra", ordem = 1 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{id}/valores", new { codigo = "MULHER", descricao = "Mulher", ordem = 0, orientacao = "Escolha conforme se identifica." })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{id}/valores", new { codigo = "ANTIGA", descricao = "Antiga", ordem = 2 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Fatos}/{id}/valores/ANTIGA")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return id;
    }

    private static object NovoFatoSensivel() => new
    {
        codigo = $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
        nome = "Identidade de gênero",
        descricao = "Como a pessoa se identifica.",
        dominio = "CATEGORICO",
        cardinalidade = "ESCALAR",
        fonteValores = "GLOBAL",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "SENSIVEL",
        finalidadeTratamento = "Verificação dos requisitos do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    private static object NovoTipoDetalhado() => new
    {
        codigo = $"TIPO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
        nome = "Laudo médico complementar",
        descricao = "Laudo que atesta a condição de saúde.",
        categoria = "SAUDE",
        formatosAceitos = "PDF",
        tamanhoMaximoMb = 5,
        classificacaoProtecao = "SENSIVEL",
        finalidadeTratamento = "Comprovação documental do candidato.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };
}
