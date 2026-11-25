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

    private static async Task<HttpResponseMessage> EnviarPaginaInvalidaAsync(Contexto contexto)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{contexto.ProcessoId}/formularios/INSCRICAO");
        request.Content = JsonContent.Create(new { faseId = await contexto.FaseDeInscricaoAsync(), titulo = "Inscrição", paginas = new[] { new { codigo = "pagina ruim", ordem = 0, tipo = "SECAO", apresentacao = new { titulo = "" } } } });
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
