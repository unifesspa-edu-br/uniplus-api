namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O endpoint <c>PUT /processos-seletivos/{id}/bonus-regional</c>, fim a fim pelo HTTP: a
/// declaração de que o processo aplica ou não o bônus é obrigatória no corpo. O host não habilita
/// <c>RespectRequiredConstructorParameters</c>, então um <c>aplica</c> ausente desserializaria como
/// <see langword="false"/> — "não aplica" por omissão —, e <c>[JsonRequired]</c> em
/// <c>DefinirBonusRegionalRequest</c> fecha essa lacuna.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirBonusRegionalEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirBonusRegionalEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Corpo sem aplica é rejeitado com 400 — omitir não declara que o processo não aplica o bônus")]
    public async Task SemAplica_400_ENadaDeclarado()
    {
        (HttpClient client, Guid processoId) = await SemearRascunhoAsync(nameof(SemAplica_400_ENadaDeclarado));

        HttpResponseMessage resposta = await PutAsync(client, processoId, """{ "regraCodigo": null }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DeclaracaoGravadaAsync(processoId)).Should().BeNull("a omissão não pode virar uma declaração");
    }

    [Fact(DisplayName = "aplica=false sem configuração declara que o processo não aplica o bônus")]
    public async Task NaoAplica_204_Declara()
    {
        (HttpClient client, Guid processoId) = await SemearRascunhoAsync(nameof(NaoAplica_204_Declara));

        HttpResponseMessage resposta = await PutAsync(client, processoId, """{ "aplica": false }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DeclaracaoGravadaAsync(processoId)).Should().BeFalse();
    }

    [Fact(DisplayName = "aplica=false com a configuração do bônus é recusado com 422 e código nomeado")]
    public async Task NaoAplicaComConfiguracao_422()
    {
        (HttpClient client, Guid processoId) = await SemearRascunhoAsync(nameof(NaoAplicaComConfiguracao_422));

        HttpResponseMessage resposta = await PutAsync(
            client, processoId, $$"""{ "aplica": false, "regraCodigo": "BONUS-MULTIPLICATIVO", "regraVersao": "v1", "fator": 1.2, "baseLegalBonusRegionalId": "{{Guid.CreateVersion7()}}" }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.bonus_regional_nao_aplica_com_configuracao");
        (await DeclaracaoGravadaAsync(processoId)).Should().BeNull();
    }

    private async Task<bool?> DeclaracaoGravadaAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == processoId);
        return processo.AplicaBonusRegional;
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, Guid processoId, string corpoJson)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/bonus-regional", UriKind.Relative))
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>
    /// Um rascunho publicável cujo bônus está por declarar: o seeder declara "não aplica" para o
    /// processo ser publicável, e o <c>complementar</c> desfaz a declaração.
    /// </summary>
    private async Task<(HttpClient Client, Guid ProcessoId)> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}", complementar: semear =>
            {
                typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.AplicaBonusRegional))!.SetValue(semear, null);
            });
        return (client, processo.Id);
    }
}
