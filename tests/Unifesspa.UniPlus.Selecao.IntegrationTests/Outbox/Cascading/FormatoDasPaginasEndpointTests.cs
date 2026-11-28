namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoFormularioPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class FormatoDasPaginasEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task Definir_PaginaInvalida_RecusaCodigoETituloSemGravar()
    {
        Contexto contexto = await SemearRascunhoAsync(fixture, nameof(Definir_PaginaInvalida_RecusaCodigoETituloSemGravar));
        string antes = await EstadoDoFormularioAsync(contexto);

        HttpResponseMessage resposta = await EnviarPaginaInvalidaAsync(contexto);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement[] erros = [.. corpo.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static erro => (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString())).Should().Equal(
            ("paginas[0].codigo", "uniplus.estrutura_formulario.pagina_codigo_invalido"),
            ("paginas[0].apresentacao.titulo", "uniplus.estrutura_formulario.pagina_titulo_invalido"));
        (await EstadoDoFormularioAsync(contexto)).Should().Be(antes);
    }

    private const string PaginaInvalida = """{ "codigo": "pagina ruim", "ordem": 0, "tipo": "SECAO", "apresentacao": { "titulo": "" } }""";

    [Theory(DisplayName = "Obrigatoriedade ausente ou nula é recusada pelo domínio, antes das recusas das páginas, e nada é gravado")]
    [InlineData($$"""{ "titulo": "Inscrição", "paginas": [{{PaginaInvalida}}] }""")]
    [InlineData($$"""{ "titulo": "Inscrição", "obrigatorio": null, "paginas": [{{PaginaInvalida}}] }""")]
    public async Task Definir_SemObrigatorio_RecusaJuntoDasPaginasSemGravar(string corpo)
    {
        Contexto contexto = await SemearRascunhoAsync(fixture, nameof(Definir_SemObrigatorio_RecusaJuntoDasPaginasSemGravar));
        string antes = await EstadoDoFormularioAsync(contexto);

        using HttpResponseMessage resposta = await EnviarAsync(contexto, corpo);

        (await ErrosAsync(resposta)).Should().Equal(
            ("obrigatorio", "uniplus.validacao.obrigatorio"),
            ("paginas[0].codigo", "uniplus.estrutura_formulario.pagina_codigo_invalido"),
            ("paginas[0].apresentacao.titulo", "uniplus.estrutura_formulario.pagina_titulo_invalido"));
        (await EstadoDoFormularioAsync(contexto)).Should().Be(antes);
    }

    [Theory(DisplayName = "Lista de páginas ausente ou nula é recusada pelo domínio, e nada é gravado")]
    [InlineData("""{ "titulo": "Inscrição", "obrigatorio": true }""")]
    [InlineData("""{ "titulo": "Inscrição", "obrigatorio": true, "paginas": null }""")]
    public async Task Definir_SemPaginas_RecusaSemGravar(string corpo)
    {
        Contexto contexto = await SemearRascunhoAsync(fixture, nameof(Definir_SemPaginas_RecusaSemGravar));
        string antes = await EstadoDoFormularioAsync(contexto);

        using HttpResponseMessage resposta = await EnviarAsync(contexto, corpo);

        (await ErrosAsync(resposta)).Should().Equal(("paginas", "uniplus.validacao.obrigatorio"));
        (await EstadoDoFormularioAsync(contexto)).Should().Be(antes);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(Contexto contexto, string corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{contexto.ProcessoId}/formularios/INSCRICAO")
        {
            Content = new StringContent(corpo, System.Text.Encoding.UTF8, "application/json"),
        };
        Autenticar(request, Autenticacao.PlataformaAdmin);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await contexto.Client.SendAsync(request);
    }

    private static async Task<(string? Campo, string? Codigo)[]> ErrosAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.GetProperty("errors").EnumerateArray().Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];
    }

    private static async Task<HttpResponseMessage> EnviarPaginaInvalidaAsync(Contexto contexto)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{contexto.ProcessoId}/formularios/INSCRICAO");
        request.Content = JsonContent.Create(new { faseId = await contexto.FaseDeInscricaoAsync(), titulo = "Inscrição", obrigatorio = true, paginas = new[] { new { codigo = "pagina ruim", ordem = 0, tipo = "SECAO", apresentacao = new { titulo = "" } } } });
        Autenticar(request, Autenticacao.PlataformaAdmin);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await contexto.Client.SendAsync(request);
    }

    private static async Task<string> EstadoDoFormularioAsync(Contexto contexto)
    {
        await using AsyncServiceScope scope = contexto.Api.Services.CreateAsyncScope();
        UniPlusDbContext banco = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<FormularioProcesso> formularios = await banco.Set<FormularioProcesso>().AsNoTracking().Include(f => f.Paginas)
            .Where(f => f.ProcessoSeletivoId == contexto.ProcessoId).ToListAsync();
        return JsonSerializer.Serialize(formularios.Select(f => new { f.Id, f.Titulo, Paginas = f.Paginas.Select(p => new { p.Id, p.Codigo, p.Titulo }) }));
    }
}
