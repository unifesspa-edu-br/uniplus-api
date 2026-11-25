namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoFormularioPeloEndpoint;

/// <summary>
/// A leitura administrativa do rascunho do formulário por finalidade (UNI-REQ-0144), fim a fim
/// pelo HTTP, sob <c>/admin/processos-seletivos/{id}/formularios/{finalidade}/apresentacao</c> (ADR-0064).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FormulariosRascunhoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FormulariosRascunhoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "GET admin do rascunho exige autenticação")]
    public async Task ObterRascunho_SemPapel_Recusa()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(ObterRascunho_SemPapel_Recusa));

        (await ctx.GetRascunhoAsync(Autenticacao.Nenhuma)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET admin do rascunho serve o formulário da configuração viva, que o público ainda não serve")]
    public async Task ObterRascunho_EmRascunho_200ComApresentacaoERegras()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(ObterRascunho_EmRascunho_200ComApresentacaoERegras));
        (await ctx.PutFormularioAsync("Formulário de Inscrição")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.PutFatosAsync([])).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage publico = await ctx.GetFormularioAsync();
        HttpResponseMessage rascunho = await ctx.GetRascunhoAsync(Autenticacao.PlataformaAdmin);

        publico.StatusCode.Should().Be(HttpStatusCode.NotFound, "o certame ainda não foi divulgado");
        rascunho.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await rascunho.Content.ReadAsStringAsync());
        ConferirRascunhoComApresentacaoERegras(doc.RootElement);
    }

    private static void ConferirRascunhoComApresentacaoERegras(JsonElement raiz)
    {
        JsonElement root = raiz.GetProperty("formulario");
        root.GetProperty("titulo").GetString().Should().Be("Formulário de Inscrição");
        root.GetProperty("coleta").GetProperty("itens").GetArrayLength().Should().Be(ConjuntoBasicoDaInscricao.Itens.Count);
        root.GetProperty("regras").GetProperty("etapas").EnumerateArray().SelectMany(static e => e.GetProperty("itens").EnumerateArray())
            .Select(static i => i.GetProperty("fatoCodigo").GetString())
            .Should().Contain("COR_RACA", "as regras da configuração viva vão com a apresentação");
        raiz.GetProperty("comprovacaoDocumental").ValueKind.Should().Be(JsonValueKind.Null, "a lista de documentos sai da publicação");
    }
}
