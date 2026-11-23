namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ApoioDoCronogramaDeFasesPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class ChavesDasExigenciasPeloEndpointTests(CascadingFixture ambienteDeTeste)
{
    [Fact]
    public async Task Gravar_ExigenciaReconhecida_PreservaIdentidadeNoBanco()
    {
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(
            ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        (Guid processo, Guid fase) = await CriarProcessoAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray pedido = Pedido(catalogos.TipoDocumentoId, fase);
        await GravarAsync(cliente, processo, pedido);
        (Guid id, string chave) = await LerExigenciaAsync(processo);
        pedido[0]!["documento"]!["chave"] = chave;

        await GravarAsync(cliente, processo, pedido);

        (await LerExigenciaAsync(processo)).Should().Be((id, chave));
    }

    [Fact]
    public async Task Gravar_ChaveDesconhecidaEDuasExigencias_FormaChavesNovas()
    {
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(
            ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        (Guid processo, Guid fase) = await CriarProcessoAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray pedido = Pedido(catalogos.TipoDocumentoId, fase);
        pedido[0]!["documento"]!["chave"] = "exigencia_desconhecida";
        pedido.Add(pedido[0]!.DeepClone());

        await GravarAsync(cliente, processo, pedido);

        string[] chaves = await LerChavesAsync(processo);
        chaves.Should().HaveCount(2);
        chaves.Should().Contain(chaves[0] + "_2");
    }

    [Fact]
    public async Task Consultar_ExigenciaGravada_DevolveChave()
    {
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(
            ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        (Guid processo, Guid fase) = await CriarProcessoAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        await GravarAsync(cliente, processo, Pedido(catalogos.TipoDocumentoId, fase));
        (_, string chave) = await LerExigenciaAsync(processo);
        using HttpRequestMessage requisicao = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processo}", UriKind.Relative));
        Autenticar(requisicao);
        using HttpResponseMessage resposta = await cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode documento = (await resposta.Content.ReadFromJsonAsync<JsonNode>())!;
        documento["documentosExigidos"]![0]!["chave"]!.GetValue<string>().Should().Be(chave);
    }

    private async Task<string[]> LerChavesAsync(Guid processo)
    {
        await using AsyncServiceScope escopo = ambienteDeTeste.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<DocumentoExigido> exigencias = await contexto.Set<DocumentoExigido>()
            .Where(item => item.ProcessoSeletivoId == processo).ToListAsync();
        return [.. exigencias.Select(static item => item.Chave.Valor).Order(StringComparer.Ordinal)];
    }

    private async Task<(Guid, Guid)> CriarProcessoAsync()
    {
        await using AsyncServiceScope escopo = ambienteDeTeste.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(contexto,
            new OpcoesDoProcessoSeletivoPublicavel { Nome = "Processo de exigências" });
        return (processo.Id, processo.CronogramaFases.First().Id);
    }

    private static JsonArray Pedido(Guid tipo, Guid fase) =>
    [
        new JsonObject
        {
            ["forma"] = "DOCUMENTO",
            ["documento"] = new JsonObject
            {
                ["tipoDocumento"] = new JsonObject { ["id"] = tipo.ToString() },
                ["momento"] = new JsonObject { ["faseId"] = fase.ToString(), ["finalidade"] = "INSCRICAO" },
                ["aplicabilidade"] = new JsonObject { ["tipo"] = "GERAL" },
                ["indeferimento"] = new JsonObject { ["obrigatorio"] = true },
                ["requisitos"] = new JsonObject { ["formatosPermitidos"] = "QUALQUER" },
            },
        },
    ];

    private static async Task GravarAsync(HttpClient cliente, Guid processo, JsonArray pedido)
    {
        using HttpRequestMessage requisicao = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processo}/documentos-exigidos", UriKind.Relative))
        { Content = JsonContent.Create(pedido) };
        Autenticar(requisicao);
        requisicao.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        using HttpResponseMessage resposta = await cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private async Task<(Guid, string)> LerExigenciaAsync(Guid processo)
    {
        await using AsyncServiceScope escopo = ambienteDeTeste.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        DocumentoExigido exigencia = await contexto.Set<DocumentoExigido>().SingleAsync(item => item.ProcessoSeletivoId == processo);
        return (exigencia.Id, exigencia.Chave.Valor);
    }
}
