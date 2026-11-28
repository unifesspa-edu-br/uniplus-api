namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDaPublicacaoDoProcessoSeletivo;

/// <summary>
/// A definição da importação do resultado pelo endpoint e o que a publicação faz com ela: a lista vazia sem a primeira fase volta a
/// declaração a por declarar, a lista vazia com a fase é aceita e recusada na publicação por falta de relatório, e a declaração completa
/// é gravada, relida do banco e publicada.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DefinirImportacaoDoResultadoEndpointTests(CascadingFixture fixture)
{
    private const string Relatorio = """{ "leiauteCodigo": "INEP_RETORNO_ENEM", "leiauteVersao": "v1" }""";

    [Fact(DisplayName = "A declaração completa é gravada, relida do banco com os relatórios e a fase, e a publicação a aceita")]
    public async Task Put_DeclaracaoCompleta_GravaERelePublica()
    {
        (Guid id, Guid documento, Guid fase) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, $$"""{ "relatorios": [{{Relatorio}}], "primeiraFaseDepoisDaImportacaoId": "{{fase}}" }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        ProcessoSeletivo relido = await LerDoBancoAsync(id);
        relido.ImportacaoDoResultado!.Relatorios.Select(static r => (r.LeiauteCodigo, r.LeiauteVersao)).Should().Equal(("INEP_RETORNO_ENEM", "v1"));
        relido.ImportacaoDoResultado.PrimeiraFaseDepoisDaImportacaoId.Should().Be(fase);
        // O processo de resultado importado não tem fase que coleta inscrição: o período de inscrição vai no pedido de publicação.
        using HttpResponseMessage publicacao = await PostPublicarAsync(client, id, documento, MakeIdempotencyKey(), comPeriodoDeInscricao: true);
        publicacao.IsSuccessStatusCode.Should().BeTrue(await publicacao.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Só com relatórios, sem a primeira fase, a declaração é gravada e relida do banco com a fase nula")]
    public async Task Put_SoRelatorios_RelePeloBancoSemFase()
    {
        (Guid id, _, _) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, $$"""{ "relatorios": [{{Relatorio}}] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        ProcessoSeletivo relido = await LerDoBancoAsync(id);
        relido.ImportacaoDoResultado!.Relatorios.Should().ContainSingle();
        relido.ImportacaoDoResultado.PrimeiraFaseDepoisDaImportacaoId.Should().BeNull();
    }

    [Fact(DisplayName = "A lista vazia sem a primeira fase desfaz a declaração, que volta a por declarar, e a publicação a recusa")]
    public async Task Put_ListaVaziaSemFase_VoltaAPorDeclararEPublicacaoRecusa()
    {
        (Guid id, Guid documento, Guid fase) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        (await PutAsync(client, id, $$"""{ "relatorios": [{{Relatorio}}], "primeiraFaseDepoisDaImportacaoId": "{{fase}}" }""")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage resposta = await PutAsync(client, id, """{ "relatorios": [] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerDoBancoAsync(id)).ImportacaoDoResultado.Should().BeNull();
        await ConferirRecusaDaPublicacaoAsync(client, id, documento, "uniplus.selecao.importacao_resultado.por_declarar");
    }

    [Fact(DisplayName = "A lista vazia com a primeira fase informada é aceita na gravação, e a publicação a recusa por falta de relatório")]
    public async Task Put_ListaVaziaComFase_AceitaEPublicacaoRecusa()
    {
        (Guid id, Guid documento, Guid fase) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, $$"""{ "relatorios": [], "primeiraFaseDepoisDaImportacaoId": "{{fase}}" }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConferirRecusaDaPublicacaoAsync(client, id, documento, "uniplus.selecao.importacao_resultado.sem_relatorio");
    }

    [Fact(DisplayName = "A primeira fase que o cronograma não tem é recusada com 422 e nada é gravado")]
    public async Task Put_FaseForaDoCronograma_Recusa422()
    {
        (Guid id, _, _) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, $$"""{ "relatorios": [{{Relatorio}}], "primeiraFaseDepoisDaImportacaoId": "{{Guid.CreateVersion7()}}" }""");

        await ConferirCodigoAsync(resposta, HttpStatusCode.UnprocessableEntity, "uniplus.selecao.importacao_resultado.primeira_fase_fora_do_cronograma");
        (await LerDoBancoAsync(id)).ImportacaoDoResultado.Should().BeNull();
    }

    [Fact(DisplayName = "O leiaute repetido é recusado com 422")]
    public async Task Put_LeiauteRepetido_Recusa422()
    {
        (Guid id, _, _) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, $$"""{ "relatorios": [{{Relatorio}}, {{Relatorio}}] }""");

        await ConferirCodigoAsync(resposta, HttpStatusCode.UnprocessableEntity, "uniplus.selecao.importacao_resultado.relatorio_repetido");
    }

    [Fact(DisplayName = "O processo com inscrição pelo sistema recusa a importação com 422")]
    public async Task Put_InscricaoPeloSistema_Recusa422()
    {
        Guid id = await SemearComOrigemAsync(OrigemCandidatos.InscricaoPropria);
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, $$"""{ "relatorios": [{{Relatorio}}] }""");

        await ConferirCodigoAsync(resposta, HttpStatusCode.UnprocessableEntity, "uniplus.selecao.importacao_resultado.em_inscricao_pelo_sistema");
    }

    [Fact(DisplayName = "O processo com inscrição pelo sistema e código malformado recebe só a recusa de origem, com 422")]
    public async Task Put_InscricaoPeloSistemaComCodigoMalformado_SoARecusaDeOrigem()
    {
        Guid id = await SemearComOrigemAsync(OrigemCandidatos.InscricaoPropria);
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, """{ "relatorios": [{ "leiauteCodigo": "MEU LEIAUTE", "leiauteVersao": "v1" }] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("code").GetString())
            .Should().Equal("uniplus.selecao.importacao_resultado.em_inscricao_pelo_sistema");
    }

    [Fact(DisplayName = "No processo importado, o código malformado e a versão ausente são recusados juntos com 422")]
    public async Task Put_CodigoMalformadoEVersaoAusente_AcumulaAsRecusas()
    {
        (Guid id, _, _) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, """{ "relatorios": [{ "leiauteCodigo": "MEU LEIAUTE", "leiauteVersao": "v1" }, { "leiauteCodigo": "OUTRO" }] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("code").GetString())
            .Should().Equal("uniplus.selecao.importacao_resultado.relatorio_sem_leiaute", "uniplus.selecao.importacao_resultado.relatorio_sem_leiaute");
    }

    [Fact(DisplayName = "O corpo sem a lista de relatórios é recusado pelo domínio com 422, no campo relatorios")]
    public async Task Put_SemListaDeRelatorios_Recusa422()
    {
        (Guid id, _, _) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, id, "{ }");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString())
            .Should().Equal("relatorios");
    }

    [Fact(DisplayName = "O processo inexistente devolve 404")]
    public async Task Put_ProcessoInexistente_Retorna404()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PutAsync(client, Guid.NewGuid(), $$"""{ "relatorios": [{{Relatorio}}] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A leitura do processo devolve a importação declarada")]
    public async Task Get_ProcessoComImportacao_DevolveABloco()
    {
        (Guid id, _, Guid fase) = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        (await PutAsync(client, id, $$"""{ "relatorios": [{{Relatorio}}], "primeiraFaseDepoisDaImportacaoId": "{{fase}}" }""")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpRequestMessage leitura = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{id}", UriKind.Relative));
        AppendTestAuth(leitura);

        using HttpResponseMessage resposta = await client.SendAsync(leitura);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement importacao = corpo.RootElement.GetProperty("importacaoDoResultado");
        importacao.GetProperty("primeiraFaseDepoisDaImportacaoId").GetGuid().Should().Be(fase);
        importacao.GetProperty("relatorios")[0].GetProperty("leiauteCodigo").GetString().Should().Be("INEP_RETORNO_ENEM");
    }

    private Task<(Guid Id, Guid Documento, Guid Fase)> SemearAsync() => SemearComOrigemEFaseAsync(OrigemCandidatos.ImportacaoExterna);

    private async Task<Guid> SemearComOrigemAsync(OrigemCandidatos origem) => (await SemearComOrigemEFaseAsync(origem)).Id;

    private async Task<(Guid Id, Guid Documento, Guid Fase)> SemearComOrigemEFaseAsync(OrigemCandidatos origem)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        bool importado = origem == OrigemCandidatos.ImportacaoExterna;
        OpcoesDoProcessoSeletivoPublicavel opcoes = new()
        {
            Nome = $"Importação do resultado {Guid.CreateVersion7()}",
            Origem = origem,
            ComBancaDeImportacaoDeCandidatos = importado,
            ComCicloDeRecurso = importado,
        };
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);
        processo.DefinirImportacaoDoResultado([], null).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
        return (processo.Id, documento.Id, processo.CronogramaFases.Single().Id);
    }

    private async Task<ProcessoSeletivo> LerDoBancoAsync(Guid id)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.Set<ProcessoSeletivo>().AsNoTracking().SingleAsync(p => p.Id == id);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{id}/importacao-resultado", UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await client.SendAsync(request);
    }

    private static async Task ConferirRecusaDaPublicacaoAsync(HttpClient client, Guid id, Guid documento, string codigo)
    {
        using HttpResponseMessage publicacao = await PostPublicarAsync(client, id, documento, MakeIdempotencyKey());
        await ConferirCodigoAsync(publicacao, HttpStatusCode.UnprocessableEntity, codigo);
    }

    private static async Task ConferirCodigoAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        resposta.StatusCode.Should().Be(status);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("code").GetString()).Should().Contain(codigo);
    }
}
