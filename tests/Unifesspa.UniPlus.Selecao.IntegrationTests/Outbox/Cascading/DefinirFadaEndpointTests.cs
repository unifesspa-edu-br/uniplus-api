namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O endpoint <c>PUT /processos-seletivos/{id}/fada</c>, fim a fim pelo HTTP: a
/// declaração de que o processo aplica ou não o FADA é obrigatória no corpo. O host não habilita
/// <c>RespectRequiredConstructorParameters</c>, então um <c>aplica</c> ausente desserializaria como
/// <see langword="false"/> — "não aplica" por omissão —, e <c>[JsonRequired]</c> em
/// <c>DefinirFadaRequest</c> fecha essa lacuna.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirFadaEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirFadaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Corpo sem aplica é rejeitado com 400 — omitir não declara que o processo não aplica o FADA")]
    public async Task SemAplica_400_ENadaDeclarado()
    {
        (HttpClient client, Guid processoId) = await SemearRascunhoAsync(nameof(SemAplica_400_ENadaDeclarado));

        HttpResponseMessage resposta = await PutAsync(client, processoId, """{ "regra": null }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DeclaracaoGravadaAsync(processoId)).Should().BeNull("a omissão não pode virar uma declaração");
    }

    [Fact(DisplayName = "aplica=false sem configuração declara que o processo não aplica o FADA")]
    public async Task NaoAplica_204_Declara()
    {
        (HttpClient client, Guid processoId) = await SemearRascunhoAsync(nameof(NaoAplica_204_Declara));

        HttpResponseMessage resposta = await PutAsync(client, processoId, """{ "aplica": false }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DeclaracaoGravadaAsync(processoId)).Should().BeFalse();
    }

    [Fact(DisplayName = "aplica=false com a configuração do FADA é recusado com 422 e código nomeado")]
    public async Task NaoAplicaComConfiguracao_422()
    {
        (HttpClient client, Guid processoId) = await SemearRascunhoAsync(nameof(NaoAplicaComConfiguracao_422));

        HttpResponseMessage resposta = await PutAsync(
            client, processoId, $$"""{ "aplica": false, "regra": { "codigo": "FADA_MULTIPLICATIVO", "versao": "v1" }, "fator": 1.2, "baseLegal": { "id": "{{Guid.CreateVersion7()}}" } }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.fada_nao_aplica_com_configuracao");
        (await DeclaracaoGravadaAsync(processoId)).Should().BeNull();
    }

    [Theory(DisplayName = "Regra informada sem versão, sem fator ou sem base legal é recusada com 422 e código nomeado, sem consultar cadastro")]
    [InlineData("""{ "aplica": true, "regra": { "codigo": "FADA_MULTIPLICATIVO" }, "fator": 1.2, "baseLegal": { "id": "019fe8f8-1400-7000-8000-000000000001" } }""")]
    [InlineData("""{ "aplica": true, "regra": { "codigo": "FADA_MULTIPLICATIVO", "versao": "v1" }, "baseLegal": { "id": "019fe8f8-1400-7000-8000-000000000001" } }""")]
    [InlineData("""{ "aplica": true, "regra": { "codigo": "FADA_MULTIPLICATIVO", "versao": "v1" }, "fator": 1.2 }""")]
    public async Task RegraSemCamposObrigatorios_422(string corpoJson)
    {
        (HttpClient client, Guid processoId) = await SemearRascunhoAsync(nameof(RegraSemCamposObrigatorios_422));

        HttpResponseMessage resposta = await PutAsync(client, processoId, corpoJson);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.configuracao_do_fada.campos_obrigatorios");
    }

    private async Task<bool?> DeclaracaoGravadaAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == processoId);
        return processo.AplicaFada;
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, Guid processoId, string corpoJson)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/fada", UriKind.Relative))
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
    /// Um rascunho publicável cujo FADA está por declarar: o seeder declara "não aplica" para o
    /// processo ser publicável, e o <c>complementar</c> desfaz a declaração.
    /// </summary>
    private async Task<(HttpClient Client, Guid ProcessoId)> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        OpcoesDoProcessoSeletivoPublicavel opcoes = new() { Nome = $"{nome} {Guid.CreateVersion7()}", Complementar = DeixarOFadaPorDeclarar };
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);

        return (api.CreateClient(), processo.Id);
    }

    private static void DeixarOFadaPorDeclarar(ProcessoSeletivo processo) =>
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.AplicaFada))!.SetValue(processo, null);
}
