namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DefinirManifestacaoDeInteresseEndpointTests(CascadingFixture fixture)
{
    [Theory(DisplayName = "PUT com um dos dois códigos responde 204 e grava a manifestação")]
    [InlineData("UMA_VEZ", ManifestacaoDeInteresse.UmaVez)]
    [InlineData("CONFIRMA_A_CADA_CHAMADA", ManifestacaoDeInteresse.ConfirmaACadaChamada)]
    public async Task Put_CodigoValido_Retorna204EGrava(string codigo, ManifestacaoDeInteresse esperada)
    {
        Guid id = await SemearAsync();

        using HttpResponseMessage resposta = await EnviarAsync(id, $$"""{ "manifestacaoDeInteresse": "{{codigo}}" }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerGravadoAsync(id)).Should().Be(esperada);
    }

    [Fact(DisplayName = "PUT com o código nulo responde 204 e volta o processo à manifestação não prevista")]
    public async Task Put_CodigoNulo_Retorna204ELimpa()
    {
        Guid id = await SemearAsync();
        (await EnviarAsync(id, """{ "manifestacaoDeInteresse": "UMA_VEZ" }""")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage resposta = await EnviarAsync(id, """{ "manifestacaoDeInteresse": null }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerGravadoAsync(id)).Should().BeNull();
    }

    [Fact(DisplayName = "PUT com código desconhecido devolve 422 no campo, sem repetir o valor, e não altera a declaração")]
    public async Task Put_CodigoDesconhecido_Retorna422()
    {
        Guid id = await SemearAsync();
        (await EnviarAsync(id, """{ "manifestacaoDeInteresse": "UMA_VEZ" }""")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage resposta = await EnviarAsync(id, """{ "manifestacaoDeInteresse": "VALOR_SECRETO_9876" }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().NotContain("VALOR_SECRETO_9876");
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString())).Should().Equal(
                ("manifestacaoDeInteresse", "uniplus.selecao.manifestacao_de_interesse.codigo_desconhecido"));
        (await LerGravadoAsync(id)).Should().Be(ManifestacaoDeInteresse.UmaVez);
    }

    [Fact(DisplayName = "PUT sem o campo da manifestação responde 400")]
    public async Task Put_SemOCampo_Retorna400()
    {
        Guid id = await SemearAsync();

        using HttpResponseMessage resposta = await EnviarAsync(id, "{}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT com um valor que não é texto responde 400")]
    public async Task Put_ValorQueNaoETexto_Retorna400()
    {
        Guid id = await SemearAsync();

        using HttpResponseMessage resposta = await EnviarAsync(id, """{ "manifestacaoDeInteresse": 7 }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT de processo inexistente responde 404")]
    public async Task Put_ProcessoInexistente_Retorna404()
    {
        using HttpResponseMessage resposta = await EnviarAsync(Guid.NewGuid(), """{ "manifestacaoDeInteresse": null }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SemearAsync()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        OpcoesDoProcessoSeletivoPublicavel opcoes = new() { Nome = $"Manifestação de interesse {Guid.CreateVersion7()}" };
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);
        return processo.Id;
    }

    private async Task<ManifestacaoDeInteresse?> LerGravadoAsync(Guid id)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == id);
        return processo.ManifestacaoDeInteresse;
    }

    private async Task<HttpResponseMessage> EnviarAsync(Guid id, string corpo)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{id}/manifestacao-interesse", UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
