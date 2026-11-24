namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

[Collection(CascadingCollection.Name)]
public sealed class RecusasDaRegraDeComprovacaoEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task DefinirDocumentosExigidos_DeclaracoesInvalidas_DevolveCodigoCampoEMensagem()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        foreach (CasoDeRecusaDaRegra caso in CasosDeRecusaDaRegra.Todos())
        {
            using HttpResponseMessage resposta = await EnviarAsync(client, processoId, caso.Regras);
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
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, [CasosDeRecusaDaRegra.RegraSemDocumento(), CasosDeRecusaDaRegra.GrupoVazio()]);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static e => e.GetProperty("field").GetString()).Should().Equal("regrasDeComprovacao[0].documento", "regrasDeComprovacao[1].itens");
        erros.Select(static e => e.GetProperty("code").GetString()).Should().Equal(
            "uniplus.selecao.regra_de_comprovacao.documento_obrigatorio", "uniplus.selecao.regra_de_comprovacao.grupo_sem_itens");
    }

    [Fact]
    public async Task DefinirDocumentosExigidos_RecusaEmGrupoDentroDeGrupo_ApontaOCaminho()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        object grupo = CasosDeRecusaDaRegra.GrupoCom([CasosDeRecusaDaRegra.RegraSemDocumento()]);
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, [grupo]);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement erro = problema.RootElement.GetProperty("errors").EnumerateArray().Single();
        erro.GetProperty("field").GetString().Should().Be("regrasDeComprovacao[0].itens[0].documento");
        erro.GetProperty("code").GetString().Should().Be("uniplus.selecao.regra_de_comprovacao.documento_obrigatorio");
    }

    [Fact]
    public async Task DefinirDocumentosExigidos_DoisDocumentosSemFormatos_ApontaCadaCaminho()
    {
        (Guid processo, Guid tipo, Guid fase) = await PrepararDocumentoAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        object regra = RegraSemFormatos(tipo, fase);
        using HttpResponseMessage resposta = await EnviarAsync(client, processo, [regra, regra]);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(static erro => erro.GetProperty("field").GetString())
            .Should().Equal("regrasDeComprovacao[0].documento.requisitos.formatosPermitidos", "regrasDeComprovacao[1].documento.requisitos.formatosPermitidos");
    }

    private async Task<(Guid Processo, Guid Tipo, Guid Fase)> PrepararDocumentoAsync()
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        TipoDocumento tipo = TipoDocumento.Criar(new DeclaracaoDoTipoDocumento($"COMPROVANTE_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}", "Comprovante", null, "IDENTIFICACAO",
            null, null, null, ClassificacaoProtecao: ClassificacaoDeProtecao.Pessoal, FinalidadeTratamento: "Comprovação documental.",
            HipoteseLegal: HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
        db.Add(tipo);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        (ProcessoSeletivo processo, DocumentoAPublicar _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"Documentos exigidos {Guid.CreateVersion7()}",
        });
        return (processo.Id, tipo.Id, processo.CronogramaFases.Single().Id);
    }

    private static object RegraSemFormatos(Guid tipo, Guid fase) => new
    {
        forma = "DOCUMENTO",
        documento = new
        {
            tipoDocumento = new { id = tipo },
            momento = new { faseId = fase },
            aplicabilidade = new { tipo = "GERAL", condicoes = Array.Empty<object>() },
            indeferimento = new { obrigatorio = true, basesLegais = Array.Empty<object>() }
        },
    };

    private static void ConferirRecusa(JsonDocument problema, CasoDeRecusaDaRegra caso)
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
