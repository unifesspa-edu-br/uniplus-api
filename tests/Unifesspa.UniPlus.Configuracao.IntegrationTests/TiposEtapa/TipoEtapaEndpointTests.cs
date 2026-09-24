namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP do cadastro de tipos de etapa (UNI-REQ-0015, UNI-REQ-0087): leitura pública de
/// ativos e manutenção exclusiva de <c>plataforma-admin</c>, com código permanentemente reservado.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoEtapaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoEtapaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-etapa é público e retorna o vendor MIME")]
    public async Task Listar_Publico_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-etapa.v1+json");
    }

    /// <summary>
    /// A carga inicial declara o que cada tipo admite, e não um valor permissivo uniforme: sem
    /// isso o wizard voltaria a oferecer peso em toda etapa, e a decisão de quem opera o cadastro
    /// dependeria da idade do banco em que ela foi feita.
    /// </summary>
    [Theory(DisplayName = "Carga inicial declara o que cada tipo semeado admite e de onde vem a nota")]
    [InlineData("ANALISE_DOCUMENTAL", false, true, false)]
    [InlineData("BANCA_HETEROIDENTIFICACAO", false, true, false)]
    [InlineData("PROVA_OBJETIVA", true, true, false)]
    [InlineData("REDACAO", true, true, false)]
    [InlineData("ENTREVISTA", true, true, false)]
    [InlineData("ANALISE_HISTORICO", true, true, false)]
    [InlineData("NOTA_ENEM", true, true, true)]
    public async Task Listar_TiposSemeados_DeclaramOCaraterQueAdmitem(
        string codigo, bool admitePontuacao, bool admiteEliminacao, bool notaDeOrigemNoEnem)
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement tipo = lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("codigo").GetString() == codigo);

        tipo.GetProperty("admitePontuacao").GetBoolean().Should().Be(admitePontuacao);
        tipo.GetProperty("admiteEliminacao").GetBoolean().Should().Be(admiteEliminacao);
        tipo.GetProperty("notaDeOrigemNoEnem").GetBoolean().Should().Be(notaDeOrigemNoEnem);
    }

    [Fact(DisplayName = "Carga inicial usa UUIDv7 RFC 9562 nos sete tipos semeados")]
    public async Task Listar_TiposSemeados_UsamUuidV7()
    {
        HashSet<string> codigosSemeados = new(StringComparer.Ordinal)
        {
            "PROVA_OBJETIVA", "REDACAO", "ENTREVISTA", "ANALISE_HISTORICO",
            "BANCA_HETEROIDENTIFICACAO", "ANALISE_DOCUMENTAL", "NOTA_ENEM",
        };
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid[] idsSemeados = lista.RootElement.EnumerateArray()
            .Where(item => codigosSemeados.Contains(item.GetProperty("codigo").GetString()!))
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

        idsSemeados.Should().HaveCount(7);
        idsSemeados.Should().OnlyHaveUniqueItems();
        idsSemeados.Should().OnlyContain(id => EhUuidV7Rfc9562(id),
            "a migration semeia entidades de domínio e o projeto exige UUIDv7");
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/tipos-etapa sem autenticação retorna 401")]
    public async Task Criar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRolePlataformaAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { codigo = CodigoUnico(), nome = "Sem permissão", admitePontuacao = true, admiteEliminacao = true });

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST cria (201), GET público lista e PUT altera somente os dados descritivos")]
    public async Task Criar_ListarEAtualizar_ComAdmin_PersisteCadastro()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            codigo,
            nome = "Seleção de teste",
            descricao = "Descrição inicial",
            admitePontuacao = true,
            admiteEliminacao = true,
        });

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage listar = await client.GetAsync(new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id)
            .GetProperty("codigo").GetString().Should().Be(codigo);

        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, new
        {
            id,
            nome = "Seleção renomeada",
            descricao = "Descrição atualizada",
            admitePontuacao = false,
            admiteEliminacao = true,
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative));
        using JsonDocument item = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        item.RootElement.GetProperty("codigo").GetString().Should().Be(codigo, "o código é a identidade imutável do tipo de etapa");
        item.RootElement.GetProperty("nome").GetString().Should().Be("Seleção renomeada");
        item.RootElement.TryGetProperty("_links", out _).Should().BeTrue("a leitura pública expõe HATEOAS nível 1");
    }

    [Theory(DisplayName = "POST recusa U+0000 nos campos textuais com 422")]
    [InlineData("codigo")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public async Task Criar_CampoTextualComCaractereNulo_Retorna422(string campo)
    {
        string invalido = $"valor{(char)0}invalido";
        object payload = campo switch
        {
            "codigo" => new { codigo = invalido, nome = "Nome válido", descricao = "Descrição válida", admitePontuacao = true, admiteEliminacao = true },
            "nome" => new { codigo = CodigoUnico(), nome = invalido, descricao = "Descrição válida", admitePontuacao = true, admiteEliminacao = true },
            "descricao" => new { codigo = CodigoUnico(), nome = "Nome válido", descricao = invalido, admitePontuacao = true, admiteEliminacao = true },
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await EnviarPostAdmin(client, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory(DisplayName = "PUT recusa U+0000 nos campos editáveis com 422")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public async Task Atualizar_CampoTextualComCaractereNulo_Retorna422(string campo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            codigo = CodigoUnico(),
            nome = "Nome válido",
            descricao = "Descrição válida",
            admitePontuacao = true,
            admiteEliminacao = true,
        });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        string invalido = $"valor{(char)0}invalido";
        object payload = campo switch
        {
            "nome" => new { id, nome = invalido, descricao = "Descrição válida", admitePontuacao = true, admiteEliminacao = true },
            "descricao" => new { id, nome = "Nome válido", descricao = invalido, admitePontuacao = true, admiteEliminacao = true },
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        HttpResponseMessage response = await EnviarPutAdmin(client, id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "ADR-0125: POST com código e nome ausentes ao mesmo tempo devolve as duas violações em errors[], campo em camelCase")]
    public async Task Criar_CodigoENomeAusentes_DevolveAsDuasViolacoesEmErrors()
    {
        var body = new { codigo = "", nome = "", admitePontuacao = true, admiteEliminacao = true };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_etapa.codigo_obrigatorio");

        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("codigo");
        errors[1].GetProperty("field").GetString().Should().Be("nome");
    }

    /// <summary>
    /// ADR-0125: prova que "codigo" genuinamente ausente do JSON (não string
    /// vazia) chega ao domínio como 422 específico, não ao 400 genérico do
    /// ASP.NET — só possível porque
    /// <see cref="Application.Commands.TiposEtapa.CriarTipoEtapaCommand.Codigo"/>
    /// é <c>string?</c>, não <c>string</c>.
    /// </summary>
    [Fact(DisplayName = "ADR-0125: POST com código genuinamente ausente do JSON chega ao domínio como 422 específico")]
    public async Task Criar_CodigoAusenteDoJson_ChegaAoDominioComoViolacaoEspecifica()
    {
        const string json = """{"nome":"Nome válido"}""";

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_etapa.codigo_obrigatorio");
    }

    [Fact(DisplayName = "PUT com Id inexistente e Nome vazio devolve 422 (validação vence sobre 404)")]
    public async Task Atualizar_IdInexistenteENomeVazio_Retorna422()
    {
        Guid id = Guid.NewGuid();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPutAdmin(client, id, new { id, nome = "", admitePontuacao = true, admiteEliminacao = true });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "DELETE desativa, remove da leitura pública e mantém o código reservado")]
    public async Task Desativar_OcultaDaLeituraPublicaESemReusoDoCodigo()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo, nome = "Seleção temporária", admitePontuacao = true, admiteEliminacao = true });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage desativar = await EnviarDeleteAdmin(client, id);
        desativar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.NotFound, "a API pública só expõe itens ativos");

        HttpResponseMessage recriar = await EnviarPostAdmin(client, new { codigo, nome = "Tentativa de reuso", admitePontuacao = true, admiteEliminacao = true });
        recriar.StatusCode.Should().Be(HttpStatusCode.Conflict, "desativar não libera a identidade regulatória do código");
    }

    [Fact(DisplayName = "POST não define a nota de origem no ENEM: o campo enviado é ignorado")]
    public async Task Criar_ComNotaDeOrigemNoEnem_Ignora()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            codigo,
            nome = "Tentativa de nota do ENEM",
            admitePontuacao = true,
            admiteEliminacao = true,
            notaDeOrigemNoEnem = true,
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using JsonDocument item = await ObterTipo(client, id);
        item.RootElement.GetProperty("notaDeOrigemNoEnem").GetBoolean().Should().BeFalse(
            "a origem da nota vem só da carga do cadastro");
    }

    [Fact(DisplayName = "PUT não muda a nota de origem no ENEM e recusa com 422 tirar a pontuação do tipo NOTA_ENEM")]
    public async Task Atualizar_TipoDoEnem_MantemAOrigemERecusaSemPontuacao()
    {
        await using TipoDoEnemRestauradoNoFim restauracao = await TipoDoEnemRestauradoNoFim.GuardarAsync(_fixture.ConnectionString);
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid id, string nome, string? descricao) = await TipoDoEnem(client);

        HttpResponseMessage semAtributo = await EnviarPutAdmin(client, id, new
        {
            id,
            nome,
            descricao,
            admitePontuacao = true,
            admiteEliminacao = true,
            notaDeOrigemNoEnem = false,
        });
        semAtributo.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (JsonDocument item = await ObterTipo(client, id))
        {
            item.RootElement.GetProperty("notaDeOrigemNoEnem").GetBoolean().Should().BeTrue(
                "o PUT não carrega a origem da nota, e o campo enviado é ignorado");
        }

        HttpResponseMessage semPontuacao = await EnviarPutAdmin(client, id, new
        {
            id,
            nome,
            descricao,
            admitePontuacao = false,
            admiteEliminacao = true,
        });
        semPontuacao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await semPontuacao.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.tipo_etapa.nota_de_origem_no_enem_exige_pontuacao");
        problema.RootElement.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("admitePontuacao");
    }

    [Fact(DisplayName = "DELETE recusa com 422 desativar o tipo NOTA_ENEM")]
    public async Task Desativar_TipoDoEnem_Recusa()
    {
        await using TipoDoEnemRestauradoNoFim restauracao = await TipoDoEnemRestauradoNoFim.GuardarAsync(_fixture.ConnectionString);
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid id, _, _) = await TipoDoEnem(client);

        HttpResponseMessage desativar = await EnviarDeleteAdmin(client, id);

        desativar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await desativar.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.tipo_etapa.nota_de_origem_no_enem_nao_desativa");
        (await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK, "o tipo continua ativo");
    }

    [Fact(DisplayName = "A restauração do tipo NOTA_ENEM desfaz o que o teste alterou na linha semeada")]
    public async Task RestauracaoDoTipoDoEnem_DesfazAAlteracao()
    {
        await using (await TipoDoEnemRestauradoNoFim.GuardarAsync(_fixture.ConnectionString))
        {
            await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
            await conexao.OpenAsync();
            await using NpgsqlCommand desativar = new(
                "UPDATE configuracao.tipos_etapa SET ativo = false, nome = 'Alterado' WHERE codigo = 'NOTA_ENEM'", conexao);
            await desativar.ExecuteNonQueryAsync();
        }

        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        (_, string nome, _) = await TipoDoEnem(client);
        nome.Should().NotBe("Alterado", "a linha volta ativa e com o nome semeado");
    }

    /// <summary>O tipo semeado, com os campos que um PUT precisa repetir para não alterá-lo.</summary>
    private static async Task<(Guid Id, string Nome, string? Descricao)> TipoDoEnem(HttpClient client)
    {
        HttpResponseMessage listar = await client.GetAsync(new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        JsonElement tipo = lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("codigo").GetString() == "NOTA_ENEM");
        string? descricao = tipo.TryGetProperty("descricao", out JsonElement valor) ? valor.GetString() : null;
        return (tipo.GetProperty("id").GetGuid(), tipo.GetProperty("nome").GetString()!, descricao);
    }

    private static async Task<JsonDocument> ObterTipo(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
    }

    private static string CodigoUnico() => $"PS_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static bool EhUuidV7Rfc9562(Guid id)
    {
        string representacao = id.ToString("D");
        return id.Version == 7 && representacao[19] is '8' or '9' or 'a' or 'b';
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/tipos-etapa/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"/api/configuracao/admin/tipos-etapa/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        return await client.SendAsync(request);
    }

    private static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    /// <summary>
    /// A linha semeada NOTA_ENEM é compartilhada pela coleção. Guarda o estado dela antes do
    /// teste e o repõe no fim, para uma regressão que a altere não contaminar os demais testes.
    /// </summary>
    private sealed class TipoDoEnemRestauradoNoFim : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly object?[] _estado;

        private TipoDoEnemRestauradoNoFim(string connectionString, object?[] estado)
        {
            _connectionString = connectionString;
            _estado = estado;
        }

        public static async Task<TipoDoEnemRestauradoNoFim> GuardarAsync(string connectionString)
        {
            await using NpgsqlConnection conexao = new(connectionString);
            await conexao.OpenAsync();
            await using NpgsqlCommand comando = new(
                "SELECT nome, descricao, ativo, admite_pontuacao, admite_eliminacao, nota_de_origem_no_enem " +
                "FROM configuracao.tipos_etapa WHERE codigo = 'NOTA_ENEM'", conexao);
            await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
            (await leitor.ReadAsync()).Should().BeTrue("pré-condição: a carga do cadastro semeia NOTA_ENEM");
            object?[] estado = new object?[leitor.FieldCount];
            leitor.GetValues(estado!);
            return new(connectionString, estado);
        }

        public async ValueTask DisposeAsync()
        {
            await using NpgsqlConnection conexao = new(_connectionString);
            await conexao.OpenAsync();
            await using NpgsqlCommand comando = new(
                "UPDATE configuracao.tipos_etapa SET nome = $1, descricao = $2, ativo = $3, admite_pontuacao = $4, " +
                "admite_eliminacao = $5, nota_de_origem_no_enem = $6 WHERE codigo = 'NOTA_ENEM'", conexao);
            foreach (object? valor in _estado)
            {
                comando.Parameters.Add(new NpgsqlParameter { Value = valor ?? DBNull.Value });
            }

            await comando.ExecuteNonQueryAsync();
        }
    }
}
