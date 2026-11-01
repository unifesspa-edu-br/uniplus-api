namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O registro permanente dos atos da proteção de dados do catálogo pelo host real (ADR-0153): cada ato fica gravado
/// e sobrevive à edição do item, a encarregada consulta as decisões com filtros e o administrador lê o histórico do item.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class HistoricoDaProtecaoEndpointTests
{
    private const string Base = "/api/configuracao/admin/protecao-do-catalogo";
    private const string Tipos = "/api/configuracao/admin/tipos-documento";
    private const string Fatos = "/api/configuracao/admin/fatos-candidato";
    private const string Finalidade = "Comprovação documental do candidato.";

    private static readonly Persona Isabella = new("isabella.figueiredo", "Isabella Priscila Figueiredo");
    private static readonly Persona Betina = new("betina.neves", "Betina Marina Valentina das Neves");
    private static readonly Persona Caroline = new("caroline.lima", "Caroline Lima");

    private readonly ConfiguracaoEndpointFixture _fixture;

    public HistoricoDaProtecaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A reprovação continua registrada depois que o administrador corrige o fato, que volta a pendente")]
    public async Task Reprovacao_ContinuaRegistradaDepoisDaCorrecao()
    {
        await PrepararPersonasAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarFatoAsync(client, $"{Marca()} Renda familiar bruta");
        (await SolicitarAsync(client, Isabella, "FATO", id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DecidirAsync(client, Betina, "FATO", id, "REPROVAR", "PESSOAL", justificativa: "A renda bruta é desnecessária para a finalidade."))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement fato = await ObterFatoAsync(client, id);
        (await Enviar(client, HttpMethod.Put, $"{Fatos}/{id}", Isabella, new { nome = fato.GetProperty("nome").GetString(), descricao = "Renda per capita da família." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterFatoAsync(client, id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("PENDENTE");
        HttpResponseMessage historico = await Enviar(client, HttpMethod.Get, $"{Fatos}/{id}/protecao/atos", Isabella);
        historico.StatusCode.Should().Be(HttpStatusCode.OK);
        historico.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.protecao-do-catalogo-ato.v1+json");
        List<JsonElement> atos = [.. (await historico.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
        atos.Select(a => a.GetProperty("ato").GetString()).Should().Equal("SOLICITACAO", "REPROVACAO");
        atos.Select(a => a.GetProperty("atorNome").GetString()).Should().Equal(Isabella.Nome, Betina.Nome);
        atos[1].GetProperty("justificativa").GetString().Should().Be("A renda bruta é desnecessária para a finalidade.");
        atos[1].GetProperty("finalidadeTratamento").GetString().Should().Be(Finalidade);
        atos.Should().OnlyContain(a => a.GetProperty("instante").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
        atos.SelectMany(a => a.EnumerateObject().Select(p => p.Name)).Should().NotContain(["sub", "atorSub", "emissor", "atorEmissor"]);

        JsonElement detalhe = await (await Enviar(client, HttpMethod.Get, $"{Base}/fatos/{id}", Betina)).Content.ReadFromJsonAsync<JsonElement>();
        detalhe.GetProperty("atos").EnumerateArray().Select(a => a.GetProperty("ato").GetString()).Should().Equal("SOLICITACAO", "REPROVACAO");
    }

    [Fact(DisplayName = "O administrador vê a solicitação, o ajuste recomendado e o aceite do tipo, em ordem")]
    public async Task Historico_DoTipo_SolicitacaoAjusteEAceite()
    {
        await PrepararPersonasAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, $"{Marca()} Laudo médico complementar");
        (await SolicitarAsync(client, Isabella, "TIPO_DOCUMENTO", id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DecidirAsync(client, Caroline, "TIPO_DOCUMENTO", id, "RECOMENDAR_AJUSTE", "INTERNO", "SENSIVEL", "Traz dado de saúde."))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Enviar(client, HttpMethod.Post, $"{Tipos}/{id}/aceite-da-recomendacao", Isabella)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Tipos}/{id}/protecao/atos", Isabella);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        List<JsonElement> atos = [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
        atos.Select(a => a.GetProperty("ato").GetString()).Should().Equal("SOLICITACAO", "AJUSTE_RECOMENDADO", "ACEITE");
        atos.Select(a => a.GetProperty("atorNome").GetString()).Should().Equal(Isabella.Nome, Caroline.Nome, Isabella.Nome);
        atos[0].GetProperty("classificacaoProtecao").GetString().Should().Be("INTERNO");
        atos[1].GetProperty("classificacaoRecomendada").GetString().Should().Be("SENSIVEL");
        atos[1].GetProperty("justificativa").GetString().Should().Be("Traz dado de saúde.");
        atos[2].GetProperty("classificacaoProtecao").GetString().Should().Be("SENSIVEL", "o aceite grava a classificação que passou a valer");
        atos[2].GetProperty("classificacaoRecomendada").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "As decisões saem das mais recentes para as mais antigas, e cada filtro restringe o recorte")]
    public async Task Decisoes_FiltrosEOrdem()
    {
        (string marca, Guid aprovado, Guid reprovado, Guid ajustado) = await RegistrarTresDecisoesAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        DateOnly hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        (await NomesAsync(client, Betina, $"busca={marca}")).Should().Equal(
            $"{marca} Laudo médico complementar", $"{marca} Renda familiar bruta", $"{marca} Certidão de nascimento");
        (await NomesAsync(client, Betina, $"busca={marca}&ato=REPROVACAO")).Should().Equal($"{marca} Renda familiar bruta");
        (await NomesAsync(client, Betina, $"busca={marca}&ato=REPROVACAO&ato=APROVACAO")).Should().Equal(
            $"{marca} Renda familiar bruta", $"{marca} Certidão de nascimento");
        (await NomesAsync(client, Betina, $"busca={marca}&cadastro=TIPO_DOCUMENTO")).Should().Equal(
            $"{marca} Laudo médico complementar", $"{marca} Certidão de nascimento");
        (await NomesAsync(client, Betina, $"busca={marca}&cadastro=FATO")).Should().Equal($"{marca} Renda familiar bruta");
        (await NomesAsync(client, Betina, $"busca={marca}&minhas=true")).Should().Equal(
            $"{marca} Renda familiar bruta", $"{marca} Certidão de nascimento");
        (await NomesAsync(client, Caroline, $"busca={marca}&minhas=true")).Should().Equal($"{marca} Laudo médico complementar");
        (await NomesAsync(client, Caroline, $"busca={marca}")).Should().HaveCount(3, "sem o filtro, a encarregada vê todas as decisões");

        (await NomesAsync(client, Betina, $"busca={marca}&de={hoje.AddDays(-1):yyyy-MM-dd}&ate={hoje.AddDays(1):yyyy-MM-dd}")).Should().HaveCount(3);
        (await NomesAsync(client, Betina, $"busca={marca}&de={hoje.AddDays(2):yyyy-MM-dd}")).Should().BeEmpty();
        (await NomesAsync(client, Betina, $"busca={marca}&ate={hoje.AddDays(-2):yyyy-MM-dd}")).Should().BeEmpty();
        (await NomesAsync(client, Betina, $"busca={marca}&ate=9999-12-31")).Should().HaveCount(3, "a data máxima não tem limite superior");

        JsonElement primeira = (await ListarDecisoesAsync(client, Betina, $"busca={marca}")).Single(d => d.GetProperty("itemId").GetGuid() == ajustado);
        primeira.GetProperty("cadastro").GetString().Should().Be("TIPO_DOCUMENTO");
        primeira.GetProperty("ato").GetString().Should().Be("AJUSTE_RECOMENDADO");
        primeira.GetProperty("classificacaoRecomendada").GetString().Should().Be("SENSIVEL");
        primeira.GetProperty("atorNome").GetString().Should().Be(Caroline.Nome);
        primeira.EnumerateObject().Select(p => p.Name).Should().NotContain(["sub", "atorSub", "emissor", "atorEmissor"]);
        aprovado.Should().NotBeEmpty();
        reprovado.Should().NotBeEmpty();
    }

    [Theory(DisplayName = "A busca compara nome e código sem distinguir maiúsculas nem acentos")]
    [InlineData("LAUDO MEDICO")]
    [InlineData("laudo médico")]
    [InlineData("LAUDO MÉDICO")]
    public async Task Decisoes_BuscaSemAcentoNemCaixa(string trecho)
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        (await NomesAsync(client, Betina, $"busca={Uri.EscapeDataString($"{marca} {trecho}")}")).Should().Equal($"{marca} Laudo médico complementar");
        (await NomesAsync(client, Betina, $"busca={Uri.EscapeDataString(marca.ToUpperInvariant())}")).Should().HaveCount(3);
    }

    [Fact(DisplayName = "A paginação percorre as decisões em páginas sem repetir nem perder nenhuma")]
    public async Task Decisoes_Paginacao()
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        List<string> percorridos = [];
        string? url = $"{Base}/decisoes?busca={marca}&limit=2";
        int paginas = 0;
        while (url is not null)
        {
            HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, url, Betina);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            percorridos.AddRange((await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(d => d.GetProperty("itemNome").GetString()!));
            paginas++;
            url = LinkRel(resposta, "next");
        }

        percorridos.Should().Equal($"{marca} Laudo médico complementar", $"{marca} Renda familiar bruta", $"{marca} Certidão de nascimento");
        paginas.Should().Be(2);
    }

    [Fact(DisplayName = "O cursor emitido para um recorte não continua outro")]
    public async Task Decisoes_CursorDeOutroRecorte_Retorna400()
    {
        (string marca, _, _, _) = await RegistrarTresDecisoesAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeira = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes?busca={marca}&limit=1", Betina);
        string proxima = LinkRel(primeira, "next")!;

        string cursor = new Uri(proxima, UriKind.RelativeOrAbsolute).Query.TrimStart('?').Split('&')
            .Single(p => p.StartsWith("cursor=", StringComparison.Ordinal));

        // O mesmo cursor, com o recorte trocado.
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes?busca={marca}&cadastro=FATO&{cursor}&direction=next", Betina);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory(DisplayName = "O filtro fora do vocabulário, ou que não é decisão, e o período invertido respondem 422 no campo")]
    [InlineData("ato=SOLICITACAO", "ato_invalido")]
    [InlineData("ato=ACEITE", "ato_invalido")]
    [InlineData("cadastro=MODALIDADE", "cadastro_invalido")]
    [InlineData("de=2026-11-02&ate=2026-11-01", "periodo_invalido")]
    public async Task Decisoes_FiltroInvalido_Retorna422(string consulta, string codigo)
    {
        await PrepararPersonasAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes?{consulta}", Betina);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain(codigo);
    }

    [Fact(DisplayName = "A lista de decisões responde 403 sem a permissão de aprovar, mesmo a quem mantém os cadastros")]
    public async Task Decisoes_SemPermissaoDeAprovar_Retorna403()
    {
        const string Paulo = "paulo.conceicao";
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Paulo);
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            _fixture.Factory.Services, Paulo, PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo,
            PermissaoConfiguracao.FatosCandidatoConsultar.Codigo);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes", new Persona(Paulo, "Paulo"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "O histórico do item responde 403 sem a permissão do cadastro e 404 quando o item não existe")]
    public async Task HistoricoDoItem_Permissoes()
    {
        const string Paulo = "paulo.conceicao";
        await PrepararPersonasAsync();
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Paulo);
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid tipo = await CadastrarTipoAsync(client, $"{Marca()} Laudo médico complementar");
        Guid fato = await CadastrarFatoAsync(client, $"{Marca()} Renda familiar bruta");
        Persona paulo = new(Paulo, "Paulo");

        // Aprovar a proteção não abre o histórico dos cadastros: ele é de quem mantém ou consulta o cadastro.
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Paulo, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo);
        (await Enviar(client, HttpMethod.Get, $"{Tipos}/{tipo}/protecao/atos", paulo)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Enviar(client, HttpMethod.Get, $"{Fatos}/{fato}/protecao/atos", paulo)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Paulo);
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            _fixture.Factory.Services, Paulo, PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoConsultar.Codigo);
        HttpResponseMessage doTipo = await Enviar(client, HttpMethod.Get, $"{Tipos}/{tipo}/protecao/atos", paulo);
        doTipo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await doTipo.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0, "o item que nunca foi solicitado não tem ato");
        (await Enviar(client, HttpMethod.Get, $"{Fatos}/{fato}/protecao/atos", paulo)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Enviar(client, HttpMethod.Get, $"{Tipos}/{Guid.NewGuid()}/protecao/atos", Isabella)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Enviar(client, HttpMethod.Get, $"{Fatos}/{Guid.NewGuid()}/protecao/atos", Isabella)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Um tipo aprovado por Betina, um fato reprovado por Betina e um tipo com ajuste recomendado por Caroline, nessa ordem.
    private async Task<(string Marca, Guid Aprovado, Guid Reprovado, Guid Ajustado)> RegistrarTresDecisoesAsync()
    {
        await PrepararPersonasAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        string marca = Marca();
        Guid aprovado = await CadastrarTipoAsync(client, $"{marca} Certidão de nascimento");
        Guid reprovado = await CadastrarFatoAsync(client, $"{marca} Renda familiar bruta");
        Guid ajustado = await CadastrarTipoAsync(client, $"{marca} Laudo médico complementar");
        (await SolicitarAsync(client, Isabella, ("TIPO_DOCUMENTO", aprovado), ("FATO", reprovado), ("TIPO_DOCUMENTO", ajustado)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await DecidirAsync(client, Betina, "TIPO_DOCUMENTO", aprovado, "APROVAR", "INTERNO")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Task.Delay(20);
        (await DecidirAsync(client, Betina, "FATO", reprovado, "REPROVAR", "PESSOAL", justificativa: "Dado desnecessário.")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Task.Delay(20);
        (await DecidirAsync(client, Caroline, "TIPO_DOCUMENTO", ajustado, "RECOMENDAR_AJUSTE", "INTERNO", "SENSIVEL")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return (marca, aprovado, reprovado, ajustado);
    }

    private static string Marca() => $"Hz{Guid.NewGuid().ToString("N")[..8]}";

    // Isabella mantém e consulta os cadastros; Betina e Caroline aprovam a proteção de dados e recebem o aviso da solicitação.
    private async Task PrepararPersonasAsync()
    {
        IServiceProvider servicos = _fixture.Factory.Services;
        foreach (Persona persona in new[] { Isabella, Betina, Caroline })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(servicos, persona.Sub);
        }

        await ConcessoesDeTeste.ConcederAsync(
            servicos,
            Isabella.Sub,
            await ConcessoesDeTeste.CriarPerfilAsync(
                servicos,
                PermissaoConfiguracao.TiposDocumentoManter.Codigo,
                PermissaoConfiguracao.FatosCandidatoManter.Codigo,
                PermissaoConfiguracao.FatosCandidatoConsultar.Codigo));

        Guid aprovadora = await ConcessoesDeTeste.CriarPerfilAsync(servicos, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(servicos, Betina.Sub, aprovadora, email: "betina.neves@unifesspa.edu.br");
        await ConcessoesDeTeste.ConcederAsync(servicos, Caroline.Sub, aprovadora, email: "caroline.lima@unifesspa.edu.br");
    }

    private static async Task<Guid> CadastrarTipoAsync(HttpClient client, string nome)
    {
        HttpResponseMessage criar = await Enviar(client, HttpMethod.Post, Tipos, Isabella, new
        {
            codigo = $"TIPO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
            nome,
            categoria = "SAUDE",
            classificacaoProtecao = "INTERNO",
            finalidadeTratamento = Finalidade,
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<Guid> CadastrarFatoAsync(HttpClient client, string nome)
    {
        HttpResponseMessage criar = await Enviar(client, HttpMethod.Post, Fatos, Isabella, new
        {
            codigo = $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
            nome,
            descricao = "Valor declarado pelo candidato.",
            dominio = "CATEGORICO",
            cardinalidade = "ESCALAR",
            fonteValores = "GLOBAL",
            pontoResolucao = "INSCRICAO",
            escopo = "CANDIDATO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = Finalidade,
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private static Task<HttpResponseMessage> SolicitarAsync(HttpClient client, Persona quem, params (string Cadastro, Guid Id)[] itens) =>
        Enviar(client, HttpMethod.Post, $"{Base}/solicitacoes", quem, new { itens = itens.Select(i => new { cadastro = i.Cadastro, i.Id }) });

    private static Task<HttpResponseMessage> SolicitarAsync(HttpClient client, Persona quem, string cadastro, Guid id) =>
        SolicitarAsync(client, quem, (cadastro, id));

    private static Task<HttpResponseMessage> DecidirAsync(
        HttpClient client, Persona quem, string cadastro, Guid itemId, string decisao, string classificacao,
        string? recomendada = null, string? justificativa = null) =>
        Enviar(client, HttpMethod.Post, Base, quem, new
        {
            cadastro,
            itemId,
            decisao,
            classificacaoProtecao = classificacao,
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
            finalidadeTratamento = Finalidade,
            classificacaoRecomendada = recomendada,
            justificativa,
        });

    private static async Task<JsonElement> ObterFatoAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Fatos}/{id}", Isabella);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<List<string>> NomesAsync(HttpClient client, Persona quem, string consulta) =>
        [.. (await ListarDecisoesAsync(client, quem, consulta)).Select(d => d.GetProperty("itemNome").GetString()!)];

    private static async Task<List<JsonElement>> ListarDecisoesAsync(HttpClient client, Persona quem, string consulta)
    {
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/decisoes?{consulta}", quem);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.protecao-do-catalogo-decisao.v1+json");
        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private static string? LinkRel(HttpResponseMessage resposta, string rel)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? links))
        {
            return null;
        }

        foreach (string parte in links.Single().Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!parte.Contains($"rel=\"{rel}\"", StringComparison.Ordinal))
            {
                continue;
            }

            int inicio = parte.IndexOf('<', StringComparison.Ordinal) + 1;
            int fim = parte.IndexOf('>', StringComparison.Ordinal);
            if (inicio > 0 && fim > inicio)
            {
                return parte[inicio..fim];
            }
        }

        return null;
    }

    private static async Task<HttpResponseMessage> Enviar(
        HttpClient client, HttpMethod metodo, string url, Persona quem, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.RelativeOrAbsolute));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, quem.Sub);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.NameHeader, quem.Nome);
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

    private sealed record Persona(string Sub, string Nome);
}
