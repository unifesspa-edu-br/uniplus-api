namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Salvar, exportar e importar a prova presencial: a exportação traz o nome que o Geo devolve, o arquivo exportado valida contra o
/// esquema, e a importação ignora o nome e cria o rascunho com os mesmos municípios, na mesma ordem.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ProvaPresencialIdaEVoltaDoRascunhoTests(CascadingFixture fixture) : IAsyncLifetime
{
    private const string Conta = "betina.neves";
    private const string Maraba = "1504208";
    private const string Belem = "1501402";

    public async Task InitializeAsync() => await ConcessoesDeTeste.RemoverDaPessoaAsync(fixture.Factory.Services, Conta);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Salvar, exportar e importar traz o nome do Geo, ignora o nome do arquivo e mantém os municípios na ordem")]
    public async Task SalvarExportarEImportar_TrazONomeDoGeoEMantemOsMunicipiosNaOrdem()
    {
        await using WebApplicationFactory<HostAssemblyMarker> host = ClienteDoRascunhoDeTeste.HostComGeo(
            fixture, new FakeConferenciaEnderecoGeo().ComMunicipio(Maraba, "PA", "Marabá").ComMunicipio(Belem, "PA", "Belém"));
        ClienteDoRascunhoDeTeste cliente = new(fixture) { HostComGeoSubstituido = host };
        await cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);

        Guid original = await cliente.CriarComDocumentoAsync(Conta, DocumentoComMunicipios());
        JsonObject exportado = await cliente.ExportarAsync(Conta, original);

        ConferirNomesDoGeoEEsquema(exportado);
        Guid copia = await ImportarComNomeDigitadoAsync(cliente, exportado);

        await ConferirCopiaAsync(cliente, copia);
    }

    private static void ConferirNomesDoGeoEEsquema(JsonObject exportado)
    {
        Municipios(exportado).Select(static m => (m["codigoIbge"]!.GetValue<string>(), m["nome"]!.GetValue<string>()))
            .Should().Equal((Maraba, "Marabá"), (Belem, "Belém"));
        new EsquemaDoDocumentoDaConfiguracao().Avaliar(exportado).Should().BeEmpty();
    }

    private static async Task<Guid> ImportarComNomeDigitadoAsync(ClienteDoRascunhoDeTeste cliente, JsonObject exportado)
    {
        Municipios(exportado)[0]["nome"] = "Nome digitado à mão";
        return await cliente.CriarComDocumentoAsync(Conta, exportado.ToJsonString());
    }

    private static async Task ConferirCopiaAsync(ClienteDoRascunhoDeTeste cliente, Guid copia)
    {
        JsonNode guardado = await cliente.DocumentoGuardadoAsync(copia);
        guardado["provaPresencial"]!["municipios"]!.AsArray().Select(static m => m!["codigoIbge"]!.GetValue<string>())
            .Should().Equal(Maraba, Belem);
        guardado["provaPresencial"]!["municipios"]!.AsArray().Should().OnlyContain(static m => !m!.AsObject().ContainsKey("nome"));
        Municipios(await cliente.ExportarAsync(Conta, copia)).Select(static m => m["nome"]!.GetValue<string>())
            .Should().Equal("Marabá", "Belém");
    }

    private static List<JsonObject> Municipios(JsonObject documento) =>
        [.. documento["provaPresencial"]!["municipios"]!.AsArray().Select(static m => m!.AsObject())];

    private static string DocumentoComMunicipios() => $$"""
        {
          "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo fictício", "unidadeAdministradoraCodigo": "CEPS" },
          "provaPresencial": { "temProvaPresencial": true, "municipios": [
            { "codigoIbge": "{{Maraba}}", "uf": "PA" }, { "codigoIbge": "{{Belem}}", "uf": "PA" }] }
        }
        """;
}
