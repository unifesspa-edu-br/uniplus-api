namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O circuito de aprovação da proteção de dados do catálogo pelo host real (ADR-0153): o item nasce pendente,
/// o administrador envia a solicitação, a proteção de dados nega com a classificação recomendada e o
/// administrador aceita, inclusive no fato semeado e de sistema.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ProtecaoDoCatalogoEndpointTests
{
    private const string Base = "/api/configuracao/admin/protecao-do-catalogo";
    private const string Tipos = "/api/configuracao/admin/tipos-documento";
    private const string Fatos = "/api/configuracao/admin/fatos-candidato";
    private const string Caroline = "caroline.lima";
    private static readonly Guid IdDeQuilombola = Guid.Parse("fa700000-0000-7000-8000-000000000002");

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ProtecaoDoCatalogoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O tipo cadastrado fica pendente; a solicitação o leva; a negativa recomenda; o aceite o aprova como sensível")]
    public async Task CicloDoTipoDeDocumento()
    {
        await TornarCarolineAprovadoraAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "INTERNO");

        (await ObterDoCatalogoAsync(client, id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("PENDENTE");

        HttpResponseMessage solicitada = await EnviarAsync(client, HttpMethod.Post, $"{Base}/solicitacoes");
        solicitada.StatusCode.Should().Be(HttpStatusCode.OK);
        (await solicitada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tiposDeDocumento").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        (await ObterDoCatalogoAsync(client, id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("SOLICITADA");

        (await DecidirAsync(client, "TIPO_DOCUMENTO", id, "NEGADA", "SENSIVEL"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement negado = await ObterDoCatalogoAsync(client, id);
        negado.GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("NEGADA");
        negado.GetProperty("aprovacao").GetProperty("classificacaoRecomendada").GetString().Should().Be("SENSIVEL");

        (await EnviarAsync(client, HttpMethod.Post, $"{Tipos}/{id}/aceite-da-recomendacao")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage tipo = await client.GetAsync(new Uri($"/api/configuracao/tipos-documento/{id}", UriKind.Relative));
        JsonElement aprovado = await tipo.Content.ReadFromJsonAsync<JsonElement>();
        aprovado.GetProperty("classificacaoProtecao").GetString().Should().Be("SENSIVEL");
        aprovado.GetProperty("protecaoAprovada").GetBoolean().Should().BeTrue();
        (await ListarAsync(client)).Should().NotContain(item => item.GetProperty("id").GetGuid() == id, "o item aprovado sai da lista");
    }

    [Fact(DisplayName = "Quem mantém os tipos e os fatos, mas não tem a permissão de aprovar, recebe 403 ao decidir e o item continua solicitado")]
    public async Task Decidir_SemPermissaoDeAprovar_Retorna403()
    {
        await TornarCarolineAprovadoraAsync();
        const string Paulo = "paulo.conceicao";
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Paulo);
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            _fixture.Factory.Services, Paulo, PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo);
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "INTERNO");
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/solicitacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement item = await ObterDoCatalogoAsync(client, id);

        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Base, UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                cadastro = "TIPO_DOCUMENTO",
                itemId = id,
                decisao = "APROVADA",
                classificacaoProtecao = item.GetProperty("classificacaoProtecao").GetString(),
                hipoteseLegal = item.GetProperty("hipoteseLegal").GetString(),
                finalidadeTratamento = item.GetProperty("finalidadeTratamento").GetString(),
            }),
        };
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Paulo);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ObterDoCatalogoAsync(client, id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("SOLICITADA");
    }

    [Fact(DisplayName = "A decisão sobre a proteção que o item deixou de ter responde 409 e não aprova a vigente")]
    public async Task Decidir_ProtecaoConferidaDiverge_Retorna409()
    {
        await TornarCarolineAprovadoraAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "INTERNO");
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/solicitacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage resposta = await DecidirAsync(client, "TIPO_DOCUMENTO", id, "APROVADA", protecaoDesatualizada: true);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("protecao_mudou").And.NotContain("conflito_de_concorrencia");
        (await ObterDoCatalogoAsync(client, id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("SOLICITADA");
    }

    [Fact(DisplayName = "A decisão sobre o item que não está numa solicitação é recusada")]
    public async Task Decidir_ItemPendente_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "PESSOAL");

        HttpResponseMessage resposta = await DecidirAsync(client, "TIPO_DOCUMENTO", id, "APROVADA");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("item_fora_de_solicitacao");
    }

    [Fact(DisplayName = "A decisão sobre o tipo que uma edição concorrente alterou responde 409 e não aprova por cima dela")]
    public async Task Decidir_TipoAlteradoConcorrentemente_Retorna409()
    {
        await TornarCarolineAprovadoraAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "INTERNO");
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/solicitacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext concorrente = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction transacao = await concorrente.Database.BeginTransactionAsync();
        // A edição concorrente segura a linha do tipo sem commitar; a decisão lê o xmin antigo e bloqueia ao gravar, e o
        // commit torna o xmin que ela leu obsoleto.
        await concorrente.Database.ExecuteSqlInterpolatedAsync($"UPDATE configuracao.tipo_documento SET updated_at = now() WHERE id = {id}");
        int pid = await ConcorrenciaTestHelpers.GetConnectionPidAsync(concorrente);

        Task<HttpResponseMessage> decisao = DecidirAsync(client, "TIPO_DOCUMENTO", id, "APROVADA");
        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(_fixture.Factory, decisao, [pid]);
        await transacao.CommitAsync();

        HttpResponseMessage resposta = await decisao;
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("conflito_de_concorrencia");
        (await ObterDoCatalogoAsync(client, id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("SOLICITADA");
    }

    [Fact(DisplayName = "No fato de sistema a hipótese legal se edita e o deixa pendente; o domínio continua travado")]
    public async Task FatoDeSistema_ProtecaoEditavelEDefinicaoTravada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonElement original = await ObterFatoAsync(client, IdDeQuilombola);

        HttpResponseMessage travada = await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original, dominio: "TEXTO"));
        travada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await travada.Content.ReadAsStringAsync()).Should().Contain("definicao_do_fato_de_sistema_travada");

        HttpResponseMessage editada = await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original, hipoteseLegal: "EXERCICIO_REGULAR_DE_DIREITOS"));
        editada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement depois = await ObterFatoAsync(client, IdDeQuilombola);
        depois.GetProperty("hipoteseLegal").GetString().Should().Be("EXERCICIO_REGULAR_DE_DIREITOS");
        depois.GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("PENDENTE");

        (await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "O aceite da recomendação aprova o fato semeado com a classificação indicada, sem mudar o código")]
    public async Task FatoSemeado_AceitaARecomendacao()
    {
        await TornarCarolineAprovadoraAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonElement original = await ObterFatoAsync(client, IdDeQuilombola);
        (await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/solicitacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await DecidirAsync(client, "FATO", IdDeQuilombola, "NEGADA", "PESSOAL"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{IdDeQuilombola}/aceite-da-recomendacao")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement aceito = await ObterFatoAsync(client, IdDeQuilombola);
        aceito.GetProperty("codigo").GetString().Should().Be("QUILOMBOLA");
        aceito.GetProperty("classificacaoProtecao").GetString().Should().Be("PESSOAL");
        aceito.GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("APROVADA");

        (await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(original))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "O detalhe do fato traz o que ele é e só os valores ativos, na ordem")]
    public async Task Detalhe_DoFato()
    {
        await TornarCarolineAprovadoraAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Fatos, new
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
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        (await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{id}/valores", new { codigo = "OUTRA", descricao = "Outra", ordem = 1 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{id}/valores", new { codigo = "MULHER", descricao = "Mulher", ordem = 0, orientacao = "Escolha conforme se identifica." })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, $"{Fatos}/{id}/valores", new { codigo = "ANTIGA", descricao = "Antiga", ordem = 2 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Fatos}/{id}/valores/ANTIGA")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/fatos/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement detalhe = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        detalhe.GetProperty("tipo").GetString().Should().Be("FATO");
        detalhe.GetProperty("nome").GetString().Should().Be("Identidade de gênero");
        detalhe.GetProperty("descricao").GetString().Should().Be("Como a pessoa se identifica.");
        detalhe.GetProperty("classificacaoProtecao").GetString().Should().Be("SENSIVEL");
        detalhe.GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("PENDENTE");
        detalhe.GetProperty("tipoDocumento").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement fato = detalhe.GetProperty("fato");
        fato.GetProperty("dominio").GetString().Should().Be("CATEGORICO");
        fato.GetProperty("cardinalidade").GetString().Should().Be("ESCALAR");
        fato.GetProperty("sistema").GetBoolean().Should().BeFalse();
        fato.GetProperty("valores").EnumerateArray().Select(v => v.GetProperty("codigo").GetString())
            .Should().Equal("MULHER", "OUTRA");
        fato.GetProperty("valores")[0].GetProperty("orientacao").GetString().Should().Be("Escolha conforme se identifica.");
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.protecao-do-catalogo-detalhe.v1+json");
    }

    [Fact(DisplayName = "O detalhe do tipo de documento traz o que é pedido ao candidato")]
    public async Task Detalhe_DoTipoDeDocumento()
    {
        await TornarCarolineAprovadoraAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Tipos, new
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
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/tipos-documento/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement detalhe = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        detalhe.GetProperty("tipo").GetString().Should().Be("TIPO_DOCUMENTO");
        detalhe.GetProperty("descricao").GetString().Should().Be("Laudo que atesta a condição de saúde.");
        detalhe.GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("PENDENTE");
        detalhe.GetProperty("fato").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement tipo = detalhe.GetProperty("tipoDocumento");
        tipo.GetProperty("categoria").GetString().Should().Be("SAUDE");
        tipo.GetProperty("formatosAceitos").GetString().Should().Be("PDF");
        tipo.GetProperty("tamanhoMaximoMb").GetInt32().Should().Be(5);
    }

    [Theory(DisplayName = "O detalhe de item inexistente, ou de cadastro que não é da fila, responde 404")]
    [InlineData("tipos-documento")]
    [InlineData("fatos")]
    [InlineData("outro-cadastro")]
    public async Task Detalhe_Inexistente_Retorna404(string cadastro)
    {
        await TornarCarolineAprovadoraAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Get, $"{Base}/{cadastro}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Quem mantém os cadastros, mas não tem a permissão de aprovar, recebe 403 no detalhe")]
    public async Task Detalhe_SemPermissaoDeAprovar_Retorna403()
    {
        await TornarCarolineAprovadoraAsync();
        const string Paulo = "paulo.conceicao";
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Paulo);
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            _fixture.Factory.Services, Paulo, PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo,
            PermissaoConfiguracao.FatosCandidatoConsultar.Codigo);
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "INTERNO");

        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"{Base}/tipos-documento/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Paulo);
        HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static object DefinicaoDe(JsonElement fato, string? dominio = null, string? hipoteseLegal = null) => new
    {
        pontoResolucao = fato.GetProperty("pontoResolucao").GetString(),
        classificacaoProtecao = fato.GetProperty("classificacaoProtecao").GetString(),
        finalidadeTratamento = fato.GetProperty("finalidadeTratamento").GetString(),
        hipoteseLegal = hipoteseLegal ?? fato.GetProperty("hipoteseLegal").GetString(),
        dominio = dominio ?? fato.GetProperty("dominio").GetString(),
        cardinalidade = fato.GetProperty("cardinalidade").GetString(),
        escopo = fato.GetProperty("escopo").GetString(),
    };

    private async Task TornarCarolineAprovadoraAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Caroline);
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Caroline, perfil, email: "caroline.lima@unifesspa.edu.br");
    }

    private static async Task<Guid> CadastrarTipoAsync(HttpClient client, string classificacao)
    {
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Tipos, new
        {
            codigo = $"TIPO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
            nome = "Laudo médico complementar",
            categoria = "SAUDE",
            classificacaoProtecao = classificacao,
            finalidadeTratamento = "Comprovação documental do candidato.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    // A decisão leva a proteção que o aprovador conferiu, como a listagem a mostra.
    private static async Task<HttpResponseMessage> DecidirAsync(
        HttpClient client, string cadastro, Guid itemId, string decisao, string? recomendada = null, bool protecaoDesatualizada = false)
    {
        JsonElement item = await ObterDoCatalogoAsync(client, itemId);
        return await EnviarAsync(client, HttpMethod.Post, Base, new
        {
            cadastro,
            itemId,
            decisao,
            classificacaoProtecao = protecaoDesatualizada ? "SENSIVEL" : item.GetProperty("classificacaoProtecao").GetString(),
            hipoteseLegal = item.GetProperty("hipoteseLegal").GetString(),
            finalidadeTratamento = item.GetProperty("finalidadeTratamento").GetString(),
            classificacaoRecomendada = recomendada,
        });
    }

    private static async Task<JsonElement> ObterDoCatalogoAsync(HttpClient client, Guid id) =>
        (await ListarAsync(client)).Single(item => item.GetProperty("id").GetGuid() == id);

    private static async Task<List<JsonElement>> ListarAsync(HttpClient client)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, Base);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private static async Task<JsonElement> ObterFatoAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Fatos}/{id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        return await client.SendAsync(request);
    }
}
