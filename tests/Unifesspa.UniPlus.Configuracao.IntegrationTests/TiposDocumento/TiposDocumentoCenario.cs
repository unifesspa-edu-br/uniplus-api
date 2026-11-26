namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

internal static class TiposDocumentoCenario
{
    internal static object DadosLaudoMedico(string codigo) => new
    {
        codigo,
        nome = "Laudo médico",
        categoria = "SAUDE",
        classificacaoProtecao = "SENSIVEL",
        finalidadeTratamento = "Comprovação do atendimento especializado.",
        hipoteseLegal = "TUTELA_DA_SAUDE",
        descricao = "Documento de saúde",
        formatosAceitos = "pdf,jpg",
        tamanhoMaximoMb = 10,
    };

    internal static async Task<Guid> ConferirCriacaoComId(HttpResponseMessage criar)
    {
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);
        return id;
    }

    internal static void ConferirConsultasBemSucedidas(HttpResponseMessage obter, HttpResponseMessage listar)
    {
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    internal static async Task ConferirCategoriaViva(HttpClient client, string codigoCategoria)
    {
        HttpResponseMessage aceito = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome = "Antes da remoção", categoria = codigoCategoria, classificacaoProtecao = "PESSOAL", finalidadeTratamento = "Comprovação documental do candidato.", hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL" });
        aceito.StatusCode.Should().Be(HttpStatusCode.Created, "a categoria estava viva");
    }

    internal static async Task<Guid> CriarTipoComCategoriaLonga(HttpClient client, string categoriaLonga)
    {
        HttpResponseMessage response = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome = "Tipo de categoria longa", categoria = categoriaLonga, classificacaoProtecao = "PESSOAL", finalidadeTratamento = "Comprovação documental do candidato.", hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    internal static void ConferirLaudoMedico(JsonElement root, string codigo)
    {
        root.GetProperty("codigo").GetString().Should().Be(codigo);
        root.GetProperty("nome").GetString().Should().Be("Laudo médico");
        root.GetProperty("categoria").GetString().Should().Be("SAUDE");
        root.GetProperty("tamanhoMaximoMb").GetInt32().Should().Be(10);
        root.GetProperty("classificacaoProtecao").GetProperty("codigo").GetString().Should().Be("SENSIVEL");
        root.GetProperty("finalidadeTratamento").GetString().Should().Be("Comprovação do atendimento especializado.");
        root.GetProperty("hipoteseLegal").GetProperty("codigo").GetString().Should().Be("TUTELA_DA_SAUDE");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    internal static void ConferirAusenciaDeRegrasMateriais(JsonElement tipo)
    {
        string[] propriedadesMateriaisProibidas =
            ["validade", "validadeMeses", "idadeMaximaMeses", "exigeAssinatura", "exigeAssinaturaDigital",
             "numeroAssinaturas", "lideranças", "lideranca", "aplicabilidade", "consequenciaDoDescumprimento"];
        foreach (string proibida in propriedadesMateriaisProibidas)
        {
            tipo.TryGetProperty(proibida, out _)
                .Should().BeFalse($"o tipo de documento é classificatório puro e não deve expor '{proibida}'");
        }
    }

    internal static void ConferirProtecaoPublica(JsonElement porId, JsonElement lista)
    {
        JsonElement[] tipos = [porId, .. lista.EnumerateArray()];

        porId.GetProperty("protecaoAprovada").GetBoolean().Should().BeFalse();
        tipos.Should().HaveCountGreaterThan(1);
        foreach (JsonElement tipo in tipos)
        {
            tipo.TryGetProperty("protecaoAprovada", out _).Should().BeTrue();
            tipo.TryGetProperty("aprovacao", out _).Should().BeFalse("a situação detalhada da aprovação não é pública");
        }
    }

    internal static async Task RemoverCategoria(HttpClient client, Guid categoriaId)
    {
        using HttpRequestMessage remover = new(
            HttpMethod.Delete, new Uri($"/api/configuracao/admin/categorias-documento/{categoriaId}", UriKind.Relative));
        AutenticarComoAdmin(remover);
        (await client.SendAsync(remover)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    internal static async Task ConferirCategoriaPersistida(HttpClient client, Guid id, string categoria)
    {
        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-documento/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("categoria").GetString().Should().Be(categoria);
    }

    internal static HttpRequestMessage CriarPostAdmin(string json)
    {
        HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-documento", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return request;
    }

    internal static string CodigoUnico() => $"DOC_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    internal static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    /// <summary>Cria uma categoria pelo cadastro e devolve o Id, para os testes que precisam removê-la depois.</summary>
    internal static async Task<Guid> CriarCategoria(HttpClient client, string codigo)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/categorias-documento", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { codigo, nome = "Categoria de teste" });

        HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    internal static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-documento", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
