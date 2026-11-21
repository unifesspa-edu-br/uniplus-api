namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

[Collection(CascadingCollection.Name)]
public sealed class RecusasDoNoDeExigenciaEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task DefinirDocumentosExigidos_DeclaracoesInvalidas_DevolveCodigoCampoEMensagem()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        foreach (CasoDeRecusaDoNo caso in CasosDeRecusaDoNo.Todos())
        {
            using HttpResponseMessage resposta = await EnviarAsync(client, processoId, caso.Nos);
            resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
            using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            ConferirRecusa(problema, caso);
        }
    }

    [Fact]
    public async Task DefinirDocumentosExigidos_ProblemasEmItensDiferentes_AcumulaAsRecusas()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, [CasosDeRecusaDoNo.FolhaSemDocumento(), CasosDeRecusaDoNo.GrupoVazio()]);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static e => e.GetProperty("field").GetString()).Should().Equal("nos[0].documento", "nos[1].filhos");
        erros.Select(static e => e.GetProperty("code").GetString()).Should().Equal(
            "uniplus.selecao.no_exigencia.documento_obrigatorio_em_folha", "uniplus.selecao.no_exigencia.grupo_vazio");
    }

    [Fact]
    public async Task DefinirDocumentosExigidos_RecusaEmGrupoAninhado_ApontaOCaminho()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        object grupo = CasosDeRecusaDoNo.GrupoCom([CasosDeRecusaDoNo.FolhaSemDocumento()]);
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, [grupo]);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement erro = problema.RootElement.GetProperty("errors").EnumerateArray().Single();
        erro.GetProperty("field").GetString().Should().Be("nos[0].filhos[0].documento");
        erro.GetProperty("code").GetString().Should().Be("uniplus.selecao.no_exigencia.documento_obrigatorio_em_folha");
    }

    private static void ConferirRecusa(JsonDocument problema, CasoDeRecusaDoNo caso)
    {
        problema.RootElement.GetProperty("code").GetString().Should().Be(caso.Codigo);
        JsonElement erro = problema.RootElement.GetProperty("errors").EnumerateArray()
            .Single(e => e.GetProperty("code").GetString() == caso.Codigo);
        erro.GetProperty("field").GetString().Should().Be(caso.Campo);
        erro.GetProperty("message").GetString().Should().Be(caso.Mensagem);
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Should().NotContain(e => e.GetProperty("code").GetString() == "uniplus.validacao");
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"Documentos exigidos {Guid.CreateVersion7()}",
        });
        return processo.Id;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, Guid processoId, object[] nos)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/documentos-exigidos", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Content = JsonContent.Create(nos);
        return await client.SendAsync(request);
    }
}
