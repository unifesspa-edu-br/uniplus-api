namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.ConferenciaDeTiposProcesso;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.RequisicoesDeTipoProcesso;


[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoProcessoConsultaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoProcessoConsultaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-processo é público e retorna o vendor MIME")]
    public async Task Listar_Publico_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-processo", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-processo.v1+json");
    }

    [Fact(DisplayName = "Carga inicial usa UUIDv7 RFC 9562 nos oito tipos legados")]
    public async Task Listar_TiposLegadosSemeados_UsamUuidV7()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-processo", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid[] idsLegados = IdentificarTiposLegados(lista);

        idsLegados.Should().HaveCount(8);
        idsLegados.Should().OnlyHaveUniqueItems();
        idsLegados.Should().OnlyContain(id => EhUuidV7Rfc9562(id),
            "a migration semeia entidades de domínio e o projeto exige UUIDv7");
    }

    [Fact(DisplayName = "GET admin/tipos-processo enxerga o desativado que a leitura pública oculta")]
    public async Task ListarParaManutencao_IncluiDesativado_QueALeituraPublicaOculta()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção a desativar" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        await EnviarDeleteAdmin(client, id);

        Guid[] idsAdmin = await LerIdsAsync(client, await EnviarGetAdmin(client, "/api/configuracao/admin/tipos-processo?limit=100"));
        Guid[] idsPublicos = await LerIdsAsync(client, await client.GetAsync(new Uri("/api/configuracao/tipos-processo?limit=100", UriKind.Relative)));

        idsAdmin.Should().Contain(id, "a visão de manutenção precisa enxergar o desativado para poder reativá-lo");
        idsPublicos.Should().NotContain(id, "a leitura pública só expõe itens ativos");
    }

    [Fact(DisplayName = "GET admin/tipos-processo?apenasAtivos=true oculta o desativado")]
    public async Task ListarParaManutencao_ApenasAtivos_OcultaDesativado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção a desativar" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        await EnviarDeleteAdmin(client, id);

        Guid[] ids = await LerIdsAsync(client, await EnviarGetAdmin(client, "/api/configuracao/admin/tipos-processo?limit=100&apenasAtivos=true"));

        ids.Should().NotContain(id);
    }

    [Fact(DisplayName = "GET admin/tipos-processo/{id} abre o desativado que a leitura pública por id oculta")]
    public async Task ObterParaManutencao_TipoDesativado_Retorna200EnquantoARotaPublicaDa404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção desativada" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        await EnviarDeleteAdmin(client, id);

        HttpResponseMessage manutencao = await EnviarGetAdmin(client, $"/api/configuracao/admin/tipos-processo/{id}");
        HttpResponseMessage publica = await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative));

        manutencao.StatusCode.Should().Be(HttpStatusCode.OK);
        publica.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument item = JsonDocument.Parse(await manutencao.Content.ReadAsStringAsync());
        item.RootElement.GetProperty("ativo").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "Links da listagem de manutenção resolvem: o self do desativado não cai em 404")]
    public async Task ListarParaManutencao_SelfDoDesativado_Resolve()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção desativada" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        await EnviarDeleteAdmin(client, id);

        HttpResponseMessage listagem = await EnviarGetAdmin(client, "/api/configuracao/admin/tipos-processo?limit=100");
        listagem.StatusCode.Should().Be(HttpStatusCode.OK);
        string self = await LerLinkProprioDoTipo(listagem, id);

        HttpResponseMessage seguindoOSelf = await EnviarGetAdmin(client, self);

        self.Should().Be($"/api/configuracao/admin/tipos-processo/{id}");
        seguindoOSelf.StatusCode.Should().Be(HttpStatusCode.OK, "o self de um item desativado precisa levar a uma representação que existe");
    }

}
