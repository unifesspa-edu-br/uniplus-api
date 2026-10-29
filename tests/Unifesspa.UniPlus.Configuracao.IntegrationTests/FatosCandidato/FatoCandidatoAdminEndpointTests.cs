namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP da manutenção do catálogo de fatos pela permissão de manter
/// (UNI-REQ-0143, ADR-0136): cadastro de fato declarado, edição de nome e descrição, desativação
/// e valores de domínio.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class FatoCandidatoAdminEndpointTests
{
    private const string Base = "/api/configuracao/admin/fatos-candidato";
    private static readonly Guid IdDeCorRaca = Guid.Parse("fa700000-0000-7000-8000-000000000001");

    private readonly ConfiguracaoEndpointFixture _fixture;

    public FatoCandidatoAdminEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Cadastro, edição, desativação e reativação do fato declarado; a lista filtra por ativo")]
    public async Task CicloDoFatoDeclarado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string codigo = CodigoUnico();

        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(codigo));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using (JsonDocument fato = await ObterAsync(client, id))
        {
            fato.RootElement.GetProperty("origem").GetString().Should().Be("DECLARADO");
            fato.RootElement.GetProperty("binding").GetString().Should().Be($"CAMPO_FORMULARIO:{codigo}");
            fato.RootElement.GetProperty("sistema").GetBoolean().Should().BeFalse();
        }

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", new { nome = "Nome editado", descricao = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{id}", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage inativos = await EnviarAsync(client, HttpMethod.Get, $"{Base}?ativo=false&origem=DECLARADO", corpo: null);
        inativos.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument lista = JsonDocument.Parse(await inativos.Content.ReadAsStringAsync()))
        {
            lista.RootElement.EnumerateArray().Should().Contain(item => item.GetProperty("id").GetGuid() == id
                && item.GetProperty("nome").GetString() == "Nome editado");
            lista.RootElement.EnumerateArray().Should().OnlyContain(item => !item.GetProperty("ativo").GetBoolean()
                && item.GetProperty("origem").GetString() == "DECLARADO");
            lista.RootElement.EnumerateArray().Should().NotContain(item => item.GetProperty("codigo").GetString() == "COR_RACA");
        }

        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/ativacao", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Origem desconhecida no filtro da lista devolve página vazia, não erro")]
    public async Task Listar_OrigemDesconhecida_PaginaVazia()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}?origem=declarado", corpo: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        lista.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "Código já usado, inclusive por fato de sistema, é recusado com conflito")]
    public async Task Criar_CodigoExistente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano("COR_RACA")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Fato de sistema não é desativado pela manutenção")]
    public async Task Desativar_FatoDeSistema_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{IdDeCorRaca}", corpo: null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Valor de domínio é acrescentado e desativado, nunca removido; desativar de novo é recusado")]
    public async Task ValoresDeDominio()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, new
        {
            codigo = CodigoUnico(),
            nome = "Forma de conclusão",
            dominio = "CATEGORICO",
            cardinalidade = "ESCALAR",
            fonteValores = "GLOBAL",
            pontoResolucao = "INSCRICAO",
            escopo = "CANDIDATO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Verificação dos requisitos do processo seletivo.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/valores", new { codigo = "EJA", descricao = "Educação de jovens e adultos", ordem = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{id}/valores/EJA", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{id}/valores/EJA", corpo: null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument fato = await ObterAsync(client, id);
        JsonElement valor = fato.RootElement.GetProperty("valores").EnumerateArray().Single();
        valor.GetProperty("codigo").GetString().Should().Be("EJA");
        valor.GetProperty("ativo").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "Valor é corrigido e reordenado pelos PUTs; o código não muda e a lista incompleta é recusada")]
    public async Task CorrecaoEReordenacaoDosValores()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, new
        {
            codigo = CodigoUnico(),
            nome = "Forma de conclusão",
            dominio = "CATEGORICO",
            cardinalidade = "ESCALAR",
            fonteValores = "GLOBAL",
            pontoResolucao = "INSCRICAO",
            escopo = "CANDIDATO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Verificação dos requisitos do processo seletivo.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/valores", new { codigo = "EJA", descricao = "EJA", ordem = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/valores", new { codigo = "REGULAR", descricao = "Regular", ordem = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/valores/EJA", new { descricao = "Educação de jovens e adultos", orientacao = "Cursou o supletivo." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/ordem-dos-valores", new { codigos = new[] { "REGULAR", "EJA" } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/ordem-dos-valores", new { codigos = new[] { "REGULAR" } }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/ordem-dos-valores", new { codigos = (string[]?)null }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/valores/NAO_EXISTE", new { descricao = "Qualquer" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument fato = await ObterAsync(client, id);
        JsonElement[] valores = [.. fato.RootElement.GetProperty("valores").EnumerateArray().OrderBy(static v => v.GetProperty("ordem").GetInt32())];
        valores.Select(static v => v.GetProperty("codigo").GetString()).Should().Equal("REGULAR", "EJA");
        valores[1].GetProperty("descricao").GetString().Should().Be("Educação de jovens e adultos");
        valores[1].GetProperty("orientacao").GetString().Should().Be("Cursou o supletivo.");
    }

    [Fact(DisplayName = "Derivado por regra é cadastrado, recebe regras padrão e as devolve como gravou")]
    public async Task DerivadoComRegrasPadrao()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string dependencia = CodigoUnico();
        (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(dependencia)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, $"{Base}/derivados", new
        {
            codigo = CodigoUnico(),
            nome = "Perfil de conclusão",
            dominio = "CATEGORICO",
            pontoResolucao = "HABILITACAO",
            escopo = "CANDIDATO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Verificação dos requisitos do processo seletivo.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/valores", new { codigo = "EJA", descricao = "Educação de jovens e adultos", ordem = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/regras-padrao", new
        {
            regras = new[] { new { contribui = "EJA", quando = new[] { new[] { new { fato = dependencia, operador = "IGUAL", valor = true } } } } },
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/regras-padrao", new { }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "corpo sem a lista não apaga as regras");
        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/regras-padrao", new
        {
            regras = new[] { new { contribui = "EJA", quando = new[] { new[] { new { fato = dependencia, operador = "IGUAL" } } } } },
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest, "condição sem valor é recusada, não vira erro interno");

        using JsonDocument fato = await ObterAsync(client, id);
        fato.RootElement.GetProperty("cardinalidade").GetString().Should().Be("MULTIVALORADO");
        JsonElement regra = fato.RootElement.GetProperty("regrasPadrao").EnumerateArray().Single();
        regra.GetProperty("contribui").GetString().Should().Be("EJA");
        JsonElement condicao = regra.GetProperty("quando").EnumerateArray().Single().EnumerateArray().Single();
        condicao.GetProperty("fato").GetString().Should().Be(dependencia);
        condicao.GetProperty("operador").GetString().Should().Be("IGUAL");
        condicao.GetProperty("valor").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Agregado sobre fato de membro é cadastrado com o vínculo ao fato de membro; sobre fato do candidato é recusado")]
    public async Task AgregadoSobreFatoDeMembro()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string membro = CodigoUnico();
        string doCandidato = CodigoUnico();
        (await EnviarAsync(client, HttpMethod.Post, Base, new
        {
            codigo = membro,
            nome = "Menor sob guarda",
            dominio = "BOOLEANO",
            cardinalidade = "ESCALAR",
            pontoResolucao = "HABILITACAO",
            escopo = "MEMBRO_GRUPO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Composição familiar.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(doCandidato))).StatusCode.Should().Be(HttpStatusCode.Created);

        object Agregado(string fatoDeMembro) => new
        {
            codigo = CodigoUnico(),
            nome = "Existe menor sob guarda",
            fatoDeMembro,
            pontoResolucao = "HABILITACAO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Composição familiar.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        };

        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, $"{Base}/agregados", Agregado(membro));
        criar.StatusCode.Should().Be(HttpStatusCode.Created, await criar.Content.ReadAsStringAsync());
        using JsonDocument fato = await ObterAsync(client, await criar.Content.ReadFromJsonAsync<Guid>());
        fato.RootElement.GetProperty("binding").GetString().Should().Be($"AGREGACAO_GRUPO:{membro}");

        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/agregados", Agregado(doCandidato)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "O contrato lido pela Seleção expõe no agregado categórico os valores do fato de membro")]
    public async Task AgregadoCategorico_ReaderExpoeOsValoresDoFatoDeMembro()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string membro = CodigoUnico();
        HttpResponseMessage criarMembro = await EnviarAsync(client, HttpMethod.Post, Base, new
        {
            codigo = membro,
            nome = "Categoria de renda",
            dominio = "CATEGORICO",
            cardinalidade = "ESCALAR",
            fonteValores = "GLOBAL",
            pontoResolucao = "HABILITACAO",
            escopo = "MEMBRO_GRUPO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Composição familiar.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criarMembro.StatusCode.Should().Be(HttpStatusCode.Created, await criarMembro.Content.ReadAsStringAsync());
        Guid idDoMembro = await criarMembro.Content.ReadFromJsonAsync<Guid>();
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{idDoMembro}/valores", new { codigo = "RURAL", descricao = "Trabalho rural", ordem = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        string agregado = CodigoUnico();
        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/agregados", new
        {
            codigo = agregado,
            nome = "Categorias de renda da família",
            fatoDeMembro = membro,
            pontoResolucao = "HABILITACAO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Composição familiar.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        IFatoCandidatoReader reader = escopo.ServiceProvider.GetRequiredService<IFatoCandidatoReader>();
        FatoCandidatoView listado = (await reader.ListarAsync()).Single(v => v.Codigo == agregado);
        FatoCandidatoView? obtido = await reader.ObterPorCodigoAsync(agregado);

        listado.ValoresDominio.Should().Equal("RURAL");
        obtido!.ValoresDominio.Should().Equal("RURAL");
    }

    [Fact(DisplayName = "A definição do fato declarado é alterada pelo PUT /definicao; código, origem e vínculo ficam")]
    public async Task AlterarDefinicao_FatoDeclarado_Retorna204()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string codigo = CodigoUnico();
        Guid id = await (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(codigo))).Content.ReadFromJsonAsync<Guid>();

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/definicao", DefinicaoCategorica())).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument fato = await ObterAsync(client, id);
        fato.RootElement.GetProperty("dominio").GetString().Should().Be("CATEGORICO");
        fato.RootElement.GetProperty("fonteValores").GetString().Should().Be("GLOBAL");
        fato.RootElement.GetProperty("pontoResolucao").GetString().Should().Be("INSCRICAO");
        fato.RootElement.GetProperty("codigo").GetString().Should().Be(codigo);
        fato.RootElement.GetProperty("binding").GetString().Should().Be($"CAMPO_FORMULARIO:{codigo}");
    }

    [Fact(DisplayName = "Eixo recusado pela definição volta como 422 no campo do eixo, e o fato não muda")]
    public async Task AlterarDefinicao_EixoIncoerente_Retorna422NoCampo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(CodigoUnico()))).Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/definicao", new
        {
            dominio = "BOOLEANO",
            cardinalidade = "ESCALAR",
            fonteValores = "GLOBAL",
            pontoResolucao = "INSCRICAO",
            escopo = "CANDIDATO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("field").GetString().Should().Be("fonteValores");
        using JsonDocument fato = await ObterAsync(client, id);
        fato.RootElement.GetProperty("dominio").GetString().Should().Be("BOOLEANO");
    }

    [Fact(DisplayName = "Alterar a definição de fato de sistema é recusado e fato inexistente é 404")]
    public async Task AlterarDefinicao_SistemaEInexistente()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{IdDeCorRaca}/definicao", DefinicaoCategorica()))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{Guid.NewGuid()}/definicao", DefinicaoCategorica()))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Alteração da definição colidindo com outra escrita do mesmo fato responde 409 e não grava")]
    public async Task AlterarDefinicao_EscritaConcorrente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(CodigoUnico()))).Content.ReadFromJsonAsync<Guid>();

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext concorrente = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        await using IDbContextTransaction transacao = await concorrente.Database.BeginTransactionAsync();
        // A escrita concorrente segura a linha do fato sem commitar; a alteração lê o xmin antigo e
        // bloqueia ao gravar, e o commit torna o xmin que ela leu obsoleto.
        await concorrente.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.rol_de_fatos_candidato SET updated_at = now() WHERE id = {id}");
        int pid = await ConcorrenciaTestHelpers.GetConnectionPidAsync(concorrente);

        Task<HttpResponseMessage> alteracao = EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/definicao", DefinicaoCategorica());
        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(_fixture.Factory, alteracao, [pid]);
        await transacao.CommitAsync();

        (await alteracao).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument fato = await ObterAsync(client, id);
        fato.RootElement.GetProperty("dominio").GetString().Should().Be("BOOLEANO");
    }

    private static object DefinicaoCategorica() => new
    {
        dominio = "CATEGORICO",
        cardinalidade = "ESCALAR",
        fonteValores = "GLOBAL",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    private static object FatoBooleano(string codigo) => new
    {
        codigo,
        nome = "Vínculo com outra instituição pública",
        dominio = "BOOLEANO",
        cardinalidade = "ESCALAR",
        pontoResolucao = "HABILITACAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    private static string CodigoUnico() => $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static async Task<JsonDocument> ObterAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}", corpo: null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo)
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
