namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoFormularioPeloEndpoint;

/// <summary>
/// A escrita administrativa do formulário por finalidade (UNI-REQ-0144), fim a fim pelo HTTP, sob
/// <c>/admin/processos-seletivos/{id}/formularios/{finalidade}</c> (ADR-0064).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FormulariosEscritaEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FormulariosEscritaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "PUT admin sem autenticação é 401")]
    public async Task Definir_SemAutenticacao_401()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_SemAutenticacao_401));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Título", autenticar: Autenticacao.Nenhuma);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "PUT admin em rascunho é 204 sem ETag e persiste o título")]
    public async Task Definir_EmRascunho_204SemEtagEPersiste()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_EmRascunho_204SemEtagEPersiste));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync("Formulário de Inscrição");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.Set<ProcessoSeletivo>().AsNoTracking().Include(static p => p.Formularios)
            .SingleAsync(p => p.Id == ctx.ProcessoId);
        processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Titulo.Should().Be("Formulário de Inscrição");
    }

    [Fact(DisplayName = "PUT admin com título acima do limite é 422 com a violação em errors[]")]
    public async Task Definir_TituloAcimaDoLimite_422()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_TituloAcimaDoLimite_422));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(new string('a', 301));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        // field usa o mesmo casing do payload JSON (camelCase, ADR-0023), não o PascalCase do C#.
        JsonElement erro = doc.RootElement.GetProperty("errors").EnumerateArray().Single();
        erro.GetProperty("field").GetString().Should().Be("titulo");
        erro.GetProperty("code").GetString().Should().Be("uniplus.estrutura_formulario.titulo_tamanho");
    }

    [Fact(DisplayName = "Finalidade fora do vocabulário é 404, na leitura e na escrita")]
    public async Task FinalidadeDesconhecida_404()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(FinalidadeDesconhecida_404));

        HttpResponseMessage escrita = await ctx.PutFormularioAsync("Título", finalidade: "MATRICULA");
        HttpResponseMessage leitura = await ctx.GetFormularioAsync(finalidade: "MATRICULA");

        escrita.StatusCode.Should().Be(HttpStatusCode.NotFound);
        leitura.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Formulário de habilitação fora da fase de habilitação é 422")]
    public async Task Definir_FaseIncoerenteComFinalidade_422()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_FaseIncoerenteComFinalidade_422));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Habilitação", finalidade: "HABILITACAO", faseId: await ctx.FaseDeInscricaoAsync());

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.fase_incoerente_com_finalidade");
    }
}
