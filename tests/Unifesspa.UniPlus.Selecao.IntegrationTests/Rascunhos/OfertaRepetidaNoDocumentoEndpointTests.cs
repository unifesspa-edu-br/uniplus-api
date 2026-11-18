namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ClienteDoRascunhoDeTeste;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class OfertaRepetidaNoDocumentoEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Oferta = "ENG_CIVIL_MARABA";
    private const string Codigo = "uniplus.selecao.processo_seletivo.oferta_curso_duplicada";
    private readonly CascadingFixture _fixture;
    private readonly ClienteDoRascunhoDeTeste _cliente;

    public OfertaRepetidaNoDocumentoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _cliente = new(fixture);
    }

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "Criar ou importar documento com oferta repetida recusa 422 e não grava rascunho")]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Post_OfertaRepetida_RecusaSemCriarRascunho(int quantidade)
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        int antes = await ContarRascunhosAsync();

        using HttpResponseMessage resposta = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Post, Rota, DocumentoComOfertas(Repetidas(quantidade)), chave: Guid.CreateVersion7().ToString());

        await ConferirRepeticoesAsync(resposta, quantidade);
        (await ContarRascunhosAsync()).Should().Be(antes);
    }

    [Theory(DisplayName = "Editar documento com oferta repetida recusa 422 e conserva o documento e a revisão")]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Put_OfertaRepetida_RecusaEConservaODocumento(int quantidade)
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await _cliente.CriarAsync(Leandro, "CEPS");
        string antes = await DocumentoGuardadoAsync(id, "\"1\"");

        using HttpResponseMessage resposta = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Put, $"{Rota}/{id}", DocumentoComOfertas(Repetidas(quantidade)), ifMatch: "\"1\"");

        await ConferirRepeticoesAsync(resposta, quantidade);
        (await DocumentoGuardadoAsync(id, "\"1\"")).Should().Be(antes);
    }

    [Theory(DisplayName = "A recusa da oferta acumula com a do esquema na ordem em que os campos foram enviados")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Post_RepeticaoEOutraRecusa_OrdenaPeloDocumento(bool esquemaAntes)
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        string documento = DocumentoComOfertas(Repetidas(2),
            antes: esquemaAntes ? "\"opcoesDeCurso\": { \"numero\": 3 }," : "",
            depois: esquemaAntes ? "" : ", \"opcoesDeCurso\": { \"numero\": 3 }");

        using HttpResponseMessage resposta = await _cliente.EnviarAsync(Leandro, HttpMethod.Post, Rota, documento, chave: Guid.CreateVersion7().ToString());

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableContent);
        JsonElement corpo = await Corpo(resposta);
        string[] campos = ["/opcoesDeCurso/numero", "/distribuicaoVagas/1/ofertaCursoCodigo"];
        corpo.GetProperty("errors").EnumerateArray().Skip(1).Select(static causa => causa.GetProperty("field").GetString())
            .Should().Equal(esquemaAntes ? campos : campos.Reverse());
        corpo.GetProperty("code").GetString().Should().Be("uniplus.selecao.documento_da_configuracao.forma_invalida");
    }

    [Fact(DisplayName = "O documento com códigos distintos pode ser criado, exportado, importado e editado")]
    public async Task Documento_OfertasDistintas_PermiteCriacaoImportacaoEEdicao()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        using HttpResponseMessage criado = await _cliente.EnviarAsync(Leandro, HttpMethod.Post, Rota,
            DocumentoComOfertas("""{ "ofertaCursoCodigo": "ENG_CIVIL_MARABA" }, { "ofertaCursoCodigo": "DIREITO_MARABA" }"""), chave: Guid.CreateVersion7().ToString());
        criado.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = (await Corpo(criado)).GetProperty("id").GetGuid();
        string exportado = await DocumentoGuardadoAsync(id, "\"1\"");
        using HttpResponseMessage importado = await _cliente.EnviarAsync(Leandro, HttpMethod.Post, Rota, exportado, chave: Guid.CreateVersion7().ToString());
        importado.StatusCode.Should().Be(HttpStatusCode.Created);
        using HttpResponseMessage editado = await _cliente.EnviarAsync(Leandro, HttpMethod.Put, $"{Rota}/{id}", exportado, ifMatch: "\"1\"");
        editado.StatusCode.Should().Be(HttpStatusCode.OK);
        editado.Headers.ETag!.Tag.Should().Be("\"2\"");
    }

    private static async Task ConferirRepeticoesAsync(HttpResponseMessage resposta, int quantidade)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableContent);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonElement corpo = await Corpo(resposta);
        corpo.GetProperty("code").GetString().Should().Be("uniplus.selecao.documento_da_configuracao.forma_invalida");
        JsonElement[] erros = [.. corpo.GetProperty("errors").EnumerateArray()];
        erros[0].TryGetProperty("field", out _).Should().BeFalse();
        JsonElement[] causas = [.. erros.Skip(1)];
        causas.Select(static causa => causa.GetProperty("field").GetString()).Should().Equal(
            Enumerable.Range(1, quantidade - 1).Select(static indice => $"/distribuicaoVagas/{indice}/ofertaCursoCodigo"));
        causas.Should().OnlyContain(static causa => causa.GetProperty("code").GetString() == Codigo);
        corpo.GetRawText().Should().NotContain(Oferta);
    }

    private async Task<string> DocumentoGuardadoAsync(Guid id, string revisao)
    {
        using HttpResponseMessage resposta = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Headers.ETag!.Tag.Should().Be(revisao);
        return await resposta.Content.ReadAsStringAsync();
    }

    private async Task<int> ContarRascunhosAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Set<RascunhoDeProcessoSeletivo>().IgnoreQueryFilters().CountAsync();
    }

    private static string Repetidas(int quantidade) => string.Join(',', Enumerable.Range(1, quantidade)
        .Select(static numero => $$"""{ "ofertaCursoCodigo": "{{Oferta}}", "vagasOfertadasBase": {{numero}} }"""));

    private static string DocumentoComOfertas(string ofertas, string antes = "", string depois = "") =>
        $$"""
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo Seletivo de Exemplo", "unidadeAdministradoraCodigo": "CEPS" },
          {{antes}} "distribuicaoVagas": [ {{ofertas}} ] {{depois}} }
        """;
}
