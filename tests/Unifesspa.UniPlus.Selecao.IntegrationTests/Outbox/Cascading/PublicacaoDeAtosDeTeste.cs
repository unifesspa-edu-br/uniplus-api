namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

internal static class PublicacaoDeAtosDeTeste
{
    internal static async Task<(Guid ProcessoId, Guid EditalAtoId)> PublicarAsync(
        CascadingApiFactory api, HttpClient client, IReadOnlyList<ProdutoDaFase>? produtosDaFase, DateTimeOffset? fimDasInscricoes)
    {
        (Guid processoId, Guid documentoEdital) = await SemearProcessoAsync(api, produtosDaFase, fimDasInscricoes);
        using HttpRequestMessage pedido = PedidoDePublicacao(processoId, documentoEdital);
        (await client.SendAsync(pedido)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Guid editalAtoId = await ObterAtoDoEditalAsync(api, processoId);
        await AtoNoRegistroCentral.LerAsync(api, editalAtoId);
        await PublicarAtoAvulsoEndpointTests.ExigirDivulgacaoAsync(api, processoId);
        return (processoId, editalAtoId);
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoAsync(
        CascadingApiFactory api, IReadOnlyList<ProdutoDaFase>? produtosDaFase, DateTimeOffset? fimDasInscricoes)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>(), new OpcoesDoProcessoSeletivoPublicavel
            { Nome = $"Atos avulsos {Guid.CreateVersion7()}", ProdutosDaFase = produtosDaFase, FimDasInscricoes = fimDasInscricoes });
        return (processo.Id, documento.Id);
    }

    private static HttpRequestMessage PedidoDePublicacao(Guid processoId, Guid documentoEdital)
    {
        HttpRequestMessage pedido = PublicarAtoAvulsoEndpointTests.Autenticado(HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao");
        pedido.Content = JsonContent.Create(new
        {
            numero = "001/2026",
            documentoEditalId = documentoEdital,
            ato = DadosDoEdital(),
        });
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return pedido;
    }

    private static object DadosDoEdital() => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2026,
        dataDoAto = PublicarAtoAvulsoEndpointTests.HojeEmTexto(),
        assinante = "Diretor do CEPS",
        tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
    };

    private static async Task<Guid> ObterAtoDoEditalAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }

    internal static async Task<JsonElement> AguardarNaPaginaPublicaAsync(HttpClient client, Guid processoId, Guid atoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            using HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/selecao/certames/{processoId}", UriKind.Relative));
            JsonElement? ato = await EncontrarAtoComDocumentosAsync(resposta, atoId);
            if (ato.HasValue)
            {
                return ato.Value;
            }
            DateTimeOffset.UtcNow.Should().BeBefore(limite, "os documentos do ato não chegaram à página pública do certame");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }

    private static async Task<JsonElement?> EncontrarAtoComDocumentosAsync(HttpResponseMessage resposta, Guid atoId)
    {
        if (resposta.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }
        JsonElement certame = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        foreach (JsonElement edital in certame.GetProperty("editaisPublicados").EnumerateArray())
        {
            if (edital.GetProperty("atoId").GetGuid() == atoId && edital.GetProperty("documentos").GetArrayLength() > 0)
            {
                return edital.Clone();
            }
        }
        return null;
    }
}
