namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// O certame divulgado na transação da publicação: o formulário e a página pública só passam a existir quando a
/// divulgação existe, e o certame é localizado pelo identificador legível congelado com o mesmo conteúdo e o mesmo selo
/// do identificador do processo.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class CertameDivulgadoNaPublicacaoTests
{
    private readonly CascadingFixture _fixture;

    public CertameDivulgadoNaPublicacaoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    // A renderização do formulário é anônima. Resolvendo pela divulgação, e não pela versão vigente por relógio, deixa de
    // ser possível distinguir rascunho de inexistente: nenhum dos dois tem linha, e a distinção respondia a um estranho,
    // numa requisição, se um identificador corresponde a um processo ainda não público.
    [Fact(DisplayName = "O formulário só é servido quando o certame é divulgado, e a recusa não distingue rascunho de inexistente")]
    public async Task Formulario_SegueADivulgacao_ENaoORelogio()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(Formulario_SegueADivulgacao_ENaoORelogio));

        (await StatusDoFormularioAsync(client, processoId)).Should().Be(HttpStatusCode.NotFound, "em rascunho o processo existe e não é público");
        (await StatusDoFormularioAsync(client, Guid.CreateVersion7())).Should().Be(HttpStatusCode.NotFound, "a mesma resposta do inexistente");
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterDivulgacaoAsync(api, processoId)).Should().NotBeNull("a divulgação nasce no commit da publicação");
        (await StatusDoFormularioAsync(client, processoId)).Should().Be(HttpStatusCode.OK);
        (await StatusDoCertameAsync(client, processoId.ToString())).Should().Be(HttpStatusCode.OK, "formulário e certame respondem sobre a mesma linha");
    }

    [Fact(DisplayName = "O certame é localizado pelo identificador legível congelado, com o mesmo conteúdo e o mesmo selo do Guid")]
    public async Task Certame_PeloIdentificadorLegivel_MesmoConteudoESeloDoGuid()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(Certame_PeloIdentificadorLegivel_MesmoConteudoESeloDoGuid));
        string identificador = await ObterIdentificadorLegivelAsync(api, processoId);
        await ExigirAMesmaRecusaAsync(client, identificador);

        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterDivulgacaoAsync(api, processoId)).Should().NotBeNull("a divulgação nasce no commit da publicação");
        string corpo = await ExigirOMesmoCertameAsync(client, processoId, identificador);
        await ExigirOMesmoIdentificadorNaVitrineAsync(api, client, processoId, identificador, corpo);
    }

    // Em rascunho, inexistente e fora do formato do cadastro: a mesma recusa.
    private static async Task ExigirAMesmaRecusaAsync(HttpClient client, string identificadorEmRascunho)
    {
        string recusaEmRascunho = await CodigoDaRecusaAsync(client, identificadorEmRascunho);

        (await CodigoDaRecusaAsync(client, "certame-que-nao-existe")).Should().Be(recusaEmRascunho);
        (await CodigoDaRecusaAsync(client, "PSIQ%202026")).Should().Be(recusaEmRascunho);
    }

    private static async Task<string> ExigirOMesmoCertameAsync(HttpClient client, Guid processoId, string identificador)
    {
        using HttpResponseMessage peloGuid = await ObterCertameAsync(client, processoId.ToString());
        using HttpResponseMessage peloIdentificador = await ObterCertameAsync(client, identificador);
        string corpo = await peloIdentificador.Content.ReadAsStringAsync();

        peloGuid.StatusCode.Should().Be(HttpStatusCode.OK);
        peloIdentificador.StatusCode.Should().Be(HttpStatusCode.OK);
        peloIdentificador.Headers.ETag.Should().Be(peloGuid.Headers.ETag);
        peloIdentificador.Headers.CacheControl!.NoCache.Should().BeTrue();
        corpo.Should().Be(await peloGuid.Content.ReadAsStringAsync());
        await ExigirRevalidacaoPeloIdentificadorAsync(client, identificador, peloGuid.Headers.ETag!);

        return corpo;
    }

    private static async Task ExigirRevalidacaoPeloIdentificadorAsync(HttpClient client, string identificador, EntityTagHeaderValue selo)
    {
        using HttpRequestMessage revalidacao = new(HttpMethod.Get, new Uri($"/api/selecao/certames/{identificador}", UriKind.Relative));
        revalidacao.Headers.IfNoneMatch.Add(selo);
        using HttpResponseMessage naoModificado = await client.SendAsync(revalidacao);

        naoModificado.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    private static async Task ExigirOMesmoIdentificadorNaVitrineAsync(
        CascadingApiFactory api, HttpClient client, Guid processoId, string identificador, string detalhe)
    {
        string nome = await ObterNomeAsync(api, processoId);
        using HttpResponseMessage vitrine = await client.GetAsync(
            new Uri($"/api/selecao/certames?limit=100&q={Uri.EscapeDataString(nome)}", UriKind.Relative), CancellationToken.None);
        vitrine.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument itens = JsonDocument.Parse(await vitrine.Content.ReadAsStringAsync());
        JsonElement item = ItensDaVitrine(itens.RootElement).Single(i => i.GetProperty("processoSeletivoId").GetGuid() == processoId);
        using JsonDocument certame = JsonDocument.Parse(detalhe);

        item.GetProperty("identificadorLegivel").GetString().Should().Be(identificador);
        certame.RootElement.GetProperty("identificadorLegivel").GetString().Should().Be(identificador, "o identificador exposto é o congelado");
        certame.RootElement.GetProperty("situacao").GetString().Should().Be(item.GetProperty("situacao").GetString(), "o mesmo vocabulário e a mesma regra da vitrine");
    }

    private static async Task<HttpStatusCode> StatusDoFormularioAsync(HttpClient client, Guid processoId)
    {
        using HttpResponseMessage resposta = await ObterFormularioAsync(client, processoId);
        return resposta.StatusCode;
    }

    private static async Task<HttpStatusCode> StatusDoCertameAsync(HttpClient client, string chave)
    {
        using HttpResponseMessage resposta = await ObterCertameAsync(client, chave);
        return resposta.StatusCode;
    }

    private static Task<HttpResponseMessage> ObterCertameAsync(HttpClient client, string chave) =>
        client.GetAsync(new Uri($"/api/selecao/certames/{chave}", UriKind.Relative), CancellationToken.None);

    private static async Task<string> CodigoDaRecusaAsync(HttpClient client, string identificador)
    {
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/selecao/certames/{identificador}", UriKind.Relative), CancellationToken.None);
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Headers.CacheControl!.NoCache.Should().BeTrue();
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return problema.RootElement.GetProperty("code").GetString()!;
    }

    private static JsonElement.ArrayEnumerator ItensDaVitrine(JsonElement raiz) =>
        raiz.ValueKind == JsonValueKind.Array
            ? raiz.EnumerateArray()
            : raiz.GetProperty("items").EnumerateArray();

    private static async Task<string> ObterNomeAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return (await db.ProcessosSeletivos.AsNoTracking().FirstAsync(p => p.Id == processoId)).Nome;
    }

    private static async Task<string> ObterIdentificadorLegivelAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().FirstAsync(p => p.Id == processoId);
        return processo.IdentificadorLegivel!.Value.Valor;
    }

    private static Task<HttpResponseMessage> ObterFormularioAsync(HttpClient client, Guid processoId) =>
        client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{processoId}/formularios/INSCRICAO", UriKind.Relative),
            CancellationToken.None);

    private static async Task<CertameDivulgado?> ObterDivulgacaoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.CertamesDivulgados.AsNoTracking().FirstOrDefaultAsync(c => c.Id == processoId);
    }
}
