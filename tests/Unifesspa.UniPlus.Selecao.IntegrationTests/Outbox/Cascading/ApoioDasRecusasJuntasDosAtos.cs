namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O que os testes das recusas juntas da publicação do ato avulso e do item do edital repetem pelo HTTP: o corpo com
/// campos que podem faltar, a leitura das causas na ordem em que a resposta as traz e o pedido de quem não tem permissão.
/// </summary>
internal static class ApoioDasRecusasJuntasDosAtos
{
    internal const string Obrigatorio = "uniplus.validacao.obrigatorio";
    internal const string DocumentoNaoEncontrado = "uniplus.selecao.ato_do_processo.documento_nao_encontrado";

    // Uma data do passado, fixa: o validador só recusa a data zero, e o teste não depende do dia em que roda.
    private const string DataDoAtoDeTeste = "2026-01-15";

    private const string ContaSemPermissaoDePublicar = "conta-sem-permissao-de-publicar";

    /// <summary>
    /// O corpo do pedido: os campos de <paramref name="campos"/> que têm valor vão com ele, e o que está nulo vai nulo; o
    /// que não está na lista não vai, porque campo ausente e campo nulo são recusas diferentes de ler.
    /// </summary>
    internal static JsonContent Corpo(IReadOnlyDictionary<string, object?> campos) => JsonContent.Create(campos);

    internal static Dictionary<string, object?> CamposDoAvulso(Guid documentoPrincipal) => new()
    {
        ["tipoAtoCodigo"] = DadosDoAtoDeTeste.TipoAviso,
        ["numero"] = "5/2026",
        ["dataDoAto"] = DataDoAtoDeTeste,
        ["assinante"] = "Diretor do CEPS",
        ["documentoPrincipalId"] = documentoPrincipal,
    };

    internal static Dictionary<string, object?> CamposDoItem(Guid documentoPrincipal) => new()
    {
        ["numero"] = "7/2026",
        ["dataDoAto"] = DataDoAtoDeTeste,
        ["assinante"] = "Diretor do CEPS",
        ["documentoPrincipalId"] = documentoPrincipal,
        ["politicaDeDivulgacaoConfirmada"] = true,
    };

    internal static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string rota, IReadOnlyDictionary<string, object?> campos)
    {
        HttpRequestMessage pedido = PublicarAtoAvulsoEndpointTests.Autenticado(HttpMethod.Post, rota);
        pedido.Content = Corpo(campos);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(pedido);
    }

    /// <summary>
    /// O pedido de quem não tem permissão para publicar: uma conta distinta da que recebeu a concessão na preparação, com o
    /// papel de candidato.
    /// </summary>
    internal static async Task<HttpResponseMessage> EnviarSemPermissaoAsync(HttpClient client, string rota, IReadOnlyDictionary<string, object?> campos)
    {
        HttpRequestMessage pedido = new(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, ContaSemPermissaoDePublicar);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "candidato");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        pedido.Content = Corpo(campos);
        return await client.SendAsync(pedido);
    }

    /// <summary>
    /// Confere o status 422, o código de cada causa e o campo de cada uma, na ordem em que a resposta as traz; o código da
    /// resposta é o da primeira causa. A causa sem campo não traz a chave `field` (ADR-0023), e `field: null` é recusado.
    /// </summary>
    internal static async Task ExigirCausasAsync(HttpResponseMessage resposta, string[] codigos, params string?[] campos)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        JsonElement problema = JsonDocument.Parse(corpo).RootElement;
        problema.GetProperty("code").GetString().Should().Be(codigos[0]);
        JsonElement[] causas = [.. problema.GetProperty("errors").EnumerateArray()];
        causas.Select(static c => c.GetProperty("code").GetString()).Should().Equal(codigos);
        causas.Select(CampoDaCausa).Should().Equal(campos);
        causas.Where(static c => c.TryGetProperty("field", out JsonElement campo) && campo.ValueKind != JsonValueKind.String)
            .Should().BeEmpty("a causa sem campo omite a chave, em vez de trazer field nulo");
    }

    private static string? CampoDaCausa(JsonElement causa) =>
        causa.TryGetProperty("field", out JsonElement campo) && campo.ValueKind == JsonValueKind.String ? campo.GetString() : null;

    internal static async Task<int> ContarAtosDoProcessoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().AtosDoProcesso.AsNoTracking()
            .CountAsync(a => a.ProcessoSeletivoId == processoId);
    }
}
