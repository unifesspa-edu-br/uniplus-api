namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PassosDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos.ClienteDoRascunhoDeTeste;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ExportacaoDoQuadroDeVagasDoRascunhoTests(CascadingFixture fixture) : IAsyncLifetime
{
    private const string Conta = "leandro.viana";
    private const int VagasOfertadas = 10;
    private const int VagasOfertadasDaLeiDeCotas = 60;
    private const decimal PercentualDaReservaLegal = 0.75m;
    private const string MediaTypeDaSimulacao = "application/vnd.uniplus.simulacao-distribuicao-vagas.v1+json";
    private readonly ClienteDoRascunhoDeTeste _cliente = new(fixture);

    public async Task InitializeAsync() => await ConcessoesDeTeste.RemoverDaPessoaAsync(fixture.Factory.Services, Conta);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A exportação traz o quadro e o total de cada distribuição calculados dos cadastros, e o documento segue válido")]
    public async Task Exportar_DistribuicaoCalculavel_TrazOQuadroEOTotal()
    {
        string oferta = await SemearOfertaAsync();
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await _cliente.CriarComDocumentoAsync(Conta, DocumentoComDistribuicao(oferta));

        JsonObject exportado = await _cliente.ExportarAsync(Conta, id);

        ConferirQuadro(exportado["distribuicaoVagas"]![0]!);
        new EsquemaDoDocumentoDaConfiguracao().Avaliar(exportado).Should().BeEmpty();
    }

    [Fact(DisplayName = "O quadro e o total exportados são iguais aos da simulação feita com os mesmos insumos")]
    public async Task Exportar_MesmosInsumosDaSimulacao_TrazOMesmoQuadroEOMesmoTotal()
    {
        Cenario cenario = await PrepararAsync(fixture.Factory);
        await CriarProcessoEmRascunhoAsync(cenario);
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await _cliente.CriarComDocumentoAsync(Conta, DocumentoDaLeiDeCotas(cenario));

        JsonNode exportada = (await _cliente.ExportarAsync(Conta, id))["distribuicaoVagas"]![0]!;
        JsonNode simulada = await SimularAsync(cenario);

        exportada["quadro"]!.AsArray().Should().HaveCount(cenario.Ctx.Catalogos.ModalidadeIdPorCodigo.Count);
        QuantidadePorModalidade(exportada).Should().BeEquivalentTo(QuantidadePorModalidade(simulada));
        exportada["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(simulada["totalDeVagasDoEdital"]!.GetValue<int>());
    }

    [Fact(DisplayName = "A distribuição cuja oferta o cadastro não conhece sai sem o quadro e sem o total, e a exportação responde 200 válido")]
    public async Task Exportar_OfertaSemCadastro_SaiSemQuadro()
    {
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await _cliente.CriarComDocumentoAsync(Conta, DocumentoComDistribuicao("OFERTA_SEM_CADASTRO"));

        JsonObject exportado = await _cliente.ExportarAsync(Conta, id);

        JsonObject distribuicao = exportado["distribuicaoVagas"]![0]!.AsObject();
        distribuicao.ContainsKey("quadro").Should().BeFalse();
        distribuicao.ContainsKey("totalDeVagasDoEdital").Should().BeFalse();
        new EsquemaDoDocumentoDaConfiguracao().Avaliar(exportado).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o Geo fora do ar a exportação ainda traz o quadro e o total")]
    public async Task Exportar_GeoForaDoAr_TrazOQuadro()
    {
        string oferta = await SemearOfertaAsync();
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo(fixture, new FakeConferenciaEnderecoGeo().Indisponivel());
        ClienteDoRascunhoDeTeste cliente = new(fixture) { HostComGeoSubstituido = host };
        await cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await cliente.CriarComDocumentoAsync(Conta, DocumentoComDistribuicao(oferta));

        JsonObject exportado = await cliente.ExportarAsync(Conta, id);

        ConferirQuadro(exportado["distribuicaoVagas"]![0]!);
    }

    [Fact(DisplayName = "Importar o arquivo exportado com o quadro e o total alterados à mão ignora a alteração, e a nova exportação traz o calculado")]
    public async Task Importar_QuadroAlteradoAMao_IgnoraEExportaOCalculado()
    {
        string oferta = await SemearOfertaAsync();
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        JsonObject exportado = await _cliente.ExportarAsync(Conta, await _cliente.CriarComDocumentoAsync(Conta, DocumentoComDistribuicao(oferta)));
        JsonObject distribuicao = exportado["distribuicaoVagas"]![0]!.AsObject();
        distribuicao["totalDeVagasDoEdital"] = 999;
        distribuicao["quadro"]![0]!["quantidade"] = 999;

        Guid copia = await _cliente.CriarComDocumentoAsync(Conta, exportado.ToJsonString());

        JsonNode guardado = await _cliente.DocumentoGuardadoAsync(copia);
        guardado["distribuicaoVagas"]![0]!.AsObject().ContainsKey("quadro").Should().BeFalse();
        ConferirQuadro((await _cliente.ExportarAsync(Conta, copia))["distribuicaoVagas"]![0]!);
    }

    [Fact(DisplayName = "A mudança de um cadastro citado pelo rascunho aparece na exportação e não muda o ETag")]
    public async Task Exportar_CadastroCitadoMudou_NaoMudaOETag()
    {
        string sufixo = await SemearCatalogosAsync();
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await _cliente.CriarComDocumentoAsync(
            Conta, DocumentoComDistribuicao(CatalogosDoEnvelopeFechado.CodigoDaOferta(sufixo)));
        string antes = await ETagDaExportacaoAsync(id);
        const string NovoNome = "Medicina com o nome alterado";

        await RenomearOCursoAsync(sufixo, NovoNome);

        (await _cliente.ExportarAsync(Conta, id))["distribuicaoVagas"]![0]!["cursoNome"]!.GetValue<string>().Should().Be(NovoNome);
        (await ETagDaExportacaoAsync(id)).Should().Be(antes);
    }

    [Fact(DisplayName = "A pessoa sem permissão recebe 404 e a exportação não traz o quadro")]
    public async Task Exportar_SemPermissao_Recusa404()
    {
        string oferta = await SemearOfertaAsync();
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await _cliente.CriarComDocumentoAsync(Conta, DocumentoComDistribuicao(oferta));
        await ConcessoesDeTeste.RemoverDaPessoaAsync(fixture.Factory.Services, Conta);

        using HttpResponseMessage resposta = await _cliente.EnviarAsync(Conta, HttpMethod.Get, $"{Rota}/{id}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("quadro");
    }

    private static Dictionary<string, int> QuantidadePorModalidade(JsonNode distribuicao) =>
        distribuicao["quadro"]!.AsArray().ToDictionary(
            static linha => linha!["modalidadeCodigo"]!.GetValue<string>(), static linha => linha!["quantidade"]!.GetValue<int>(), StringComparer.Ordinal);

    private static async Task<JsonNode> SimularAsync(Cenario cenario)
    {
        using HttpResponseMessage resposta = await cenario.Ctx.EnviarAsync(
            HttpMethod.Post, $"{ContextoDoEnvelopeFechado.Rota}/{cenario.Ctx.ProcessoId}/distribuicao-vagas/simulacao",
            new[] { InsumosDaSimulacao(cenario) }, idempotente: false, accept: MediaTypeDaSimulacao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return JsonNode.Parse(await resposta.Content.ReadAsStringAsync())![0]!;
    }

    private static object InsumosDaSimulacao(Cenario cenario) => new
    {
        ofertaCursoId = cenario.Ctx.Catalogos.OfertaCursoId,
        vagasOfertadasBase = VagasOfertadasDaLeiDeCotas,
        percentualDaReservaLegal = PercentualDaReservaLegal,
        regraDistribuicaoCodigo = RegraDeDistribuicaoDeVagas.Lei12711.Codigo(),
        regraDistribuicaoVersao = "v1",
        regraAjusteCodigo = RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11.Codigo(),
        regraAjusteVersao = "v1",
        referenciaReservaDemograficaId = cenario.Ctx.Catalogos.ReferenciaReservaDemograficaId,
        modalidadeIds = cenario.Ctx.Catalogos.ModalidadeIdPorCodigo.Values.ToArray(),
        quadro = Array.Empty<object>(),
    };

    private static string DocumentoDaLeiDeCotas(Cenario cenario)
    {
        string modalidades = string.Join(", ", cenario.Ctx.Catalogos.ModalidadeIdPorCodigo.Keys.Select(static codigo => $$"""{ "codigo": "{{codigo}}" }"""));

        return $$"""
            {
              "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "identificacao": { "nome": "Processo fictício", "unidadeAdministradoraCodigo": "CEPS" },
              "distribuicaoVagas": [ { "ofertaCursoCodigo": "{{CatalogosDoEnvelopeFechado.CodigoDaOferta(cenario.Sufixo)}}", "vagasOfertadasBase": {{VagasOfertadasDaLeiDeCotas}},
                "percentualDaReservaLegal": {{PercentualDaReservaLegal.ToString(CultureInfo.InvariantCulture)}},
                "regraDistribuicao": { "codigo": "{{RegraDeDistribuicaoDeVagas.Lei12711.Codigo()}}", "versao": "v1" },
                "regraAjuste": { "codigo": "{{RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11.Codigo()}}", "versao": "v1" },
                "referenciaDemograficaCodigo": "{{CatalogosDoEnvelopeFechado.CodigoDaReferenciaDemografica(cenario.Sufixo)}}",
                "modalidades": [ {{modalidades}} ] } ]
            }
            """;
    }

    private static void ConferirQuadro(JsonNode distribuicao)
    {
        distribuicao["cursoNome"]!.GetValue<string>().Should().Be("Medicina");
        JsonArray quadro = distribuicao["quadro"]!.AsArray();
        quadro.Should().ContainSingle();
        quadro[0]!["modalidadeCodigo"]!.GetValue<string>().Should().Be(ModalidadesFederaisLei12711.Ac);
        quadro[0]!["quantidade"]!.GetValue<int>().Should().Be(VagasOfertadas);
        distribuicao["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(VagasOfertadas);
    }

    private async Task<string> ETagDaExportacaoAsync(Guid id)
    {
        using HttpResponseMessage resposta = await _cliente.EnviarAsync(Conta, HttpMethod.Get, $"{Rota}/{id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return resposta.Headers.ETag!.Tag;
    }

    private async Task<string> SemearCatalogosAsync()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, sufixo);
        return sufixo;
    }

    private async Task<string> SemearOfertaAsync() => CatalogosDoEnvelopeFechado.CodigoDaOferta(await SemearCatalogosAsync());

    private async Task RenomearOCursoAsync(string sufixo, string nome)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string codigo = CatalogosDoEnvelopeFechado.CodigoDoCurso(sufixo);
        await db.Database.ExecuteSqlAsync($"UPDATE configuracao.cursos SET nome = {nome} WHERE codigo = {codigo}");
    }

    private static string DocumentoComDistribuicao(string oferta) => $$"""
        {
          "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo fictício", "unidadeAdministradoraCodigo": "CEPS" },
          "distribuicaoVagas": [ { "ofertaCursoCodigo": "{{oferta}}", "vagasOfertadasBase": {{VagasOfertadas}},
            "regraDistribuicao": { "codigo": "{{RegraDeDistribuicaoDeVagas.Institucional.Codigo()}}", "versao": "v1" },
            "modalidades": [ { "codigo": "{{ModalidadesFederaisLei12711.Ac}}", "quantidadeDeclarada": {{VagasOfertadas}} } ] } ]
        }
        """;
}
