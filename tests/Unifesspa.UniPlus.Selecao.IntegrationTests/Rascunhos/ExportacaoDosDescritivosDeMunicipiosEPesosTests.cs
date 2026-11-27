namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ExportacaoDosDescritivosDeMunicipiosEPesosTests(CascadingFixture fixture) : IAsyncLifetime
{
    private const string Conta = "leandro.viana";
    private readonly ClienteDoRascunhoDeTeste _cliente = new(fixture);

    public async Task InitializeAsync() => await ConcessoesDeTeste.RemoverDaPessoaAsync(fixture.Factory.Services, Conta);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExportarEImportar_ComResolucaoDePesosCadastrada_TrazOQuadroDosQuatroGruposESoPreservaAResolucao()
    {
        string resolucao = await PesosAreaEnemSeeder.SemearResolucaoAsync(fixture.Factory);
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid original = await _cliente.CriarComDocumentoAsync(Conta, DocumentoComResolucao(resolucao));
        JsonObject primeiro = await _cliente.ExportarAsync(Conta, original);
        ConferirQuadro(primeiro["classificacao"]!["quadroPesoAreaEnem"]!.AsArray());
        new EsquemaDoDocumentoDaConfiguracao().Avaliar(primeiro).Should().BeEmpty();
        Guid copia = await _cliente.CriarComDocumentoAsync(Conta, primeiro.ToJsonString());
        JsonNode guardado = await _cliente.DocumentoGuardadoAsync(original);
        guardado["classificacao"]!.AsObject().ContainsKey("quadroPesoAreaEnem").Should().BeFalse();
        JsonNode.DeepEquals(guardado, await _cliente.DocumentoGuardadoAsync(copia)).Should().BeTrue();
        await ConferirExportacaoDaCopiaAsync(primeiro, copia);
    }

    [Fact]
    public async Task Exportar_ComGeoForaDoAr_DevolveOsMunicipiosSemNomeEComOCodigoQueOsIdentifica()
    {
        await using WebApplicationFactory<HostAssemblyMarker> host = ClienteDoRascunhoDeTeste.HostComGeo(
            fixture, new FakeConferenciaEnderecoGeo().Indisponivel());
        ClienteDoRascunhoDeTeste cliente = new(fixture) { HostComGeoSubstituido = host };
        await cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await cliente.CriarComDocumentoAsync(Conta, DocumentoComMunicipioDeProva());
        JsonObject exportado = await cliente.ExportarAsync(Conta, id);
        JsonObject municipio = exportado["provaPresencial"]!["municipios"]![0]!.AsObject();
        municipio.ContainsKey("nome").Should().BeFalse();
        municipio["codigoIbge"]!.GetValue<string>().Should().Be("1504208");
        new EsquemaDoDocumentoDaConfiguracao().Avaliar(exportado).Should().BeEmpty();
    }

    private async Task ConferirExportacaoDaCopiaAsync(JsonObject primeiro, Guid copia)
    {
        JsonObject segundo = await _cliente.ExportarAsync(Conta, copia);
        segundo["id"]!.GetValue<Guid>().Should().Be(copia);
        primeiro.Remove("id");
        segundo.Remove("id");
        JsonNode.DeepEquals(primeiro, segundo).Should().BeTrue();
    }

    private static void ConferirQuadro(JsonArray quadro)
    {
        quadro.Select(static grupo => grupo!["grupoAreaEnemCodigo"]!.GetValue<string>())
            .Should().Equal(PesosAreaEnemSeeder.TodosOsGrupos.Order(StringComparer.Ordinal));
        quadro.Should().OnlyContain(static grupo => grupo!["areas"]!.AsArray().Count == 5);
        JsonNode redacao = AreaDe(quadro[0]!, PesoAreaEnem.CodigoRedacao);
        redacao["peso"]!.GetValue<decimal>().Should().Be(2.00m);
        redacao["corte"]!.GetValue<decimal>().Should().Be(400m);
        JsonNode linguagens = AreaDe(quadro[0]!, PesoAreaEnem.CodigoLinguagens);
        linguagens["peso"]!.GetValue<decimal>().Should().Be(2.50m);
        linguagens.AsObject().ContainsKey("corte").Should().BeFalse();
    }

    private static JsonNode AreaDe(JsonNode grupo, string codigo) =>
        grupo["areas"]!.AsArray().Single(area => area!["codigo"]!.GetValue<string>() == codigo)!;

    private static string DocumentoComResolucao(string resolucao) => $$"""
        {
          "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo fictício", "unidadeAdministradoraCodigo": "CEPS" },
          "classificacao": { "baseadoEmEnem": true, "resolucaoPesoAreaEnem": "{{resolucao}}" }
        }
        """;

    private static string DocumentoComMunicipioDeProva() => """
        {
          "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo fictício", "unidadeAdministradoraCodigo": "CEPS" },
          "provaPresencial": { "temProvaPresencial": true, "municipios": [{ "codigoIbge": "1504208", "uf": "PA" }] }
        }
        """;
}
