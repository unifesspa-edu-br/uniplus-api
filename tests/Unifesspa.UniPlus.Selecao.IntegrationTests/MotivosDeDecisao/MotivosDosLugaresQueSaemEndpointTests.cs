namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Net;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao.ChamadasDosMotivosDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CicloDaRetificacaoApoio;

/// <summary>
/// A etapa que sai do processo leva junto os motivos pendentes dela, e o motivo já publicado recusa a saída
/// (UNI-REQ-0124, UNI-REQ-0285), pelos endpoints reais contra Postgres via Testcontainers.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class MotivosDosLugaresQueSaemEndpointTests : IAsyncLifetime
{
    private static readonly Guid TipoProvaObjetiva = new("019fee1e-7000-7000-8000-000000000001");

    private readonly CascadingFixture _fixture;
    private readonly ChamadasDosMotivosDeTeste _chamadas;

    public MotivosDosLugaresQueSaemEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
        _chamadas = new ChamadasDosMotivosDeTeste(fixture, "TEST_MLQ");
    }

    public Task InitializeAsync() => TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);

    public Task DisposeAsync() => _chamadas.LimparAsync();

    [Fact(DisplayName = "No rascunho, substituir a etapa sem devolver o id leva o motivo dela; devolvendo o id, o motivo continua")]
    public async Task Rascunho_EtapaSemId_LevaOMotivo_ComId_Mantem()
    {
        (Guid processoId, _) = await _chamadas.SemearProcessoAsync(nameof(Rascunho_EtapaSemId_LevaOMotivo_ComId_Mantem));
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        Guid etapa = await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, etapa, motivo.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage comId = await SubstituirEtapaAsync(processoId, etapa);
        comId.StatusCode.Should().Be(HttpStatusCode.NoContent, await comId.Content.ReadAsStringAsync());
        (await _chamadas.LerAsync(processoId)).Should().ContainSingle("a etapa devolvida com o id é a mesma, e leva o motivo consigo");

        using HttpResponseMessage semId = await SubstituirEtapaAsync(processoId, id: null);
        semId.StatusCode.Should().Be(HttpStatusCode.NoContent, await semId.Content.ReadAsStringAsync());
        (await _chamadas.LerAsync(processoId)).Should().BeEmpty("a etapa nova nasce sem motivo, e o da etapa que saiu foi junto");
    }

    [Fact(DisplayName = "Na retificação, a etapa que sai com motivo já publicado recusa com 422, o estado e o ETag ficam intactos, e a mesma chave devolve a mesma recusa")]
    public async Task Retificacao_EtapaComMotivoPublicado_Recusa_EstadoIntacto()
    {
        (Guid processoId, Guid etapa) = await PublicarComMotivoNaEtapaAsync(nameof(Retificacao_EtapaComMotivoPublicado_Recusa_EstadoIntacto));
        using HttpClient client = _fixture.Factory.CreateClient();
        CicloDaRetificacao ciclo = new(_fixture.Factory, client, processoId);
        string etag = LerETag(await ciclo.AbrirAsync("Troca da etapa"));
        string chave = Guid.CreateVersion7().ToString("N");

        using HttpResponseMessage recusada = await SubstituirEtapaAsync(processoId, id: null, etag, chave);
        using HttpResponseMessage repetida = await SubstituirEtapaAsync(processoId, id: null, etag, chave);

        await ConferirRecusaDoMotivoPublicadoAsync(recusada);
        await ConferirRecusaDoMotivoPublicadoAsync(repetida);
        LerETag(await ciclo.ObterSessaoAsync()).Should().Be(etag, "a recusa não avança a revisão da sessão");
        (await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId)).Should().Be(etapa, "a etapa continua a mesma");
        (await _chamadas.LerAsync(processoId)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Na retificação, a etapa nova com motivo pendente que sai leva o motivo, e o descarte da sessão segue funcionando")]
    public async Task Retificacao_EtapaNovaComMotivoPendente_Sai_DescarteFunciona()
    {
        (Guid processoId, Guid documentoId) = await _chamadas.SemearProcessoAsync(nameof(Retificacao_EtapaNovaComMotivoPendente_Sai_DescarteFunciona));
        await _chamadas.PublicarAsync(processoId, documentoId);
        using HttpClient client = _fixture.Factory.CreateClient();
        CicloDaRetificacao ciclo = new(_fixture.Factory, client, processoId);
        string etagComMotivo = await IncluirMotivoNaEtapaNovaAsync(processoId, LerETag(await ciclo.AbrirAsync("Troca da etapa")));

        using HttpResponseMessage substituida = await SubstituirEtapaAsync(processoId, id: null, etagComMotivo);
        substituida.StatusCode.Should().Be(HttpStatusCode.NoContent, await substituida.Content.ReadAsStringAsync());
        (await _chamadas.LerAsync(processoId)).Should().BeEmpty();

        using HttpResponseMessage descarte = await ciclo.DescartarAsync(LerETag(substituida));
        descarte.IsSuccessStatusCode.Should().BeTrue(await descarte.Content.ReadAsStringAsync());
        (await _chamadas.LerAsync(processoId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "No rascunho, remover a fase do cronograma leva os motivos escolhidos nela e nas etapas dela")]
    public async Task Rascunho_FaseQueSai_LevaOsMotivosDaFaseEDasEtapas()
    {
        (Guid processoId, _) = await SemearProcessoComFaseQueSaiAsync(nameof(Rascunho_FaseQueSai_LevaOsMotivosDaFaseEDasEtapas));
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        Guid fase = await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId, ProcessoComFaseQueSaiDeTeste.CodigoDaFaseQueSai);
        Guid etapa = await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaFase(processoId, fase, motivo.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, etapa, motivo.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage substituido = await SubstituirCronogramaAsync(processoId);

        substituido.StatusCode.Should().Be(HttpStatusCode.NoContent, await substituido.Content.ReadAsStringAsync());
        (await _chamadas.LerAsync(processoId)).Should().BeEmpty("a fase saiu, e a etapa dela com ela");
    }

    [Fact(DisplayName = "Na retificação, a fase que sai do cronograma com motivo já publicado recusa com 422, o estado e o ETag ficam intactos, e a mesma chave devolve a mesma recusa")]
    public async Task Retificacao_FaseComMotivoPublicado_Recusa_EstadoIntacto()
    {
        (Guid processoId, Guid fase) = await PublicarComMotivoNaFaseAsync(nameof(Retificacao_FaseComMotivoPublicado_Recusa_EstadoIntacto));
        using HttpClient client = _fixture.Factory.CreateClient();
        CicloDaRetificacao ciclo = new(_fixture.Factory, client, processoId);
        string etag = LerETag(await ciclo.AbrirAsync("Troca do cronograma"));
        string chave = Guid.CreateVersion7().ToString("N");

        using HttpResponseMessage recusada = await SubstituirCronogramaAsync(processoId, etag, chave);
        using HttpResponseMessage repetida = await SubstituirCronogramaAsync(processoId, etag, chave);

        await ConferirRecusaDoMotivoPublicadoAsync(recusada);
        await ConferirRecusaDoMotivoPublicadoAsync(repetida);
        LerETag(await ciclo.ObterSessaoAsync()).Should().Be(etag, "a recusa não avança a revisão da sessão");
        (await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId, ProcessoComFaseQueSaiDeTeste.CodigoDaFaseQueSai)).Should().Be(fase, "a fase continua no cronograma");
        (await _chamadas.LerAsync(processoId)).Should().ContainSingle();
    }

    private async Task<(Guid ProcessoId, Guid Fase)> PublicarComMotivoNaFaseAsync(string nome)
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoComFaseQueSaiAsync(nome);
        await _chamadas.AdiarAsFasesAsync(processoId);
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        Guid fase = await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId, ProcessoComFaseQueSaiDeTeste.CodigoDaFaseQueSai);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaFase(processoId, fase, motivo.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _chamadas.PublicarAsync(processoId, documentoId);

        return (processoId, fase);
    }

    private Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoComFaseQueSaiAsync(string nome) =>
        ProcessoComFaseQueSaiDeTeste.SemearAsync(_chamadas.Services, $"{nome} {Guid.CreateVersion7()}");

    /// <summary>Substitui o cronograma só pela fase da inscrição: a fase de avaliação sai, e a etapa dela com ela.</summary>
    private async Task<HttpResponseMessage> SubstituirCronogramaAsync(Guid processoId, string? etag = null, string? chave = null) =>
        await _chamadas.EnviarAsync(
            HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/cronograma-fases", new[] { await FaseDaInscricaoAsync() }, etag, chave);

    private async Task<object> FaseDaInscricaoAsync() => new
    {
        ordem = 1,
        faseCanonicaId = await LugaresDoProcessoDeTeste.FaseDoCatalogoAsync(_chamadas.Services, ProcessoComFaseQueSaiDeTeste.CodigoDaFaseQueFica),
        inicio = new DateTimeOffset(2030, 1, 10, 0, 0, 0, TimeSpan.Zero),
        fim = new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
        produtos = Array.Empty<object>(),
        faseConcluinteCodigo = (string?)null,
        emiteParecerIndividual = false,
        bancasRequeridas = Array.Empty<object>(),
        regraRecurso = (object?)null,
    };

    private async Task<(Guid ProcessoId, Guid Etapa)> PublicarComMotivoNaEtapaAsync(string nome)
    {
        (Guid processoId, Guid documentoId) = await _chamadas.SemearProcessoAsync(nome);
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        Guid etapa = await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, etapa, motivo.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _chamadas.PublicarAsync(processoId, documentoId);

        return (processoId, etapa);
    }

    private async Task<string> IncluirMotivoNaEtapaNovaAsync(Guid processoId, string etag)
    {
        using HttpResponseMessage nova = await SubstituirEtapaAsync(processoId, id: null, etag);
        nova.StatusCode.Should().Be(HttpStatusCode.NoContent, await nova.Content.ReadAsStringAsync());
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        Guid etapaNova = await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId);
        using HttpResponseMessage inclusao = await _chamadas.IncluirAsync(
            LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, etapaNova, motivo.Id), "Retorno do INEP passou a ser exigido pela norma.", LerETag(nova));
        inclusao.StatusCode.Should().Be(HttpStatusCode.NoContent, await inclusao.Content.ReadAsStringAsync());

        return LerETag(inclusao);
    }

    private static async Task ConferirRecusaDoMotivoPublicadoAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        (await CodigoDoErroAsync(resposta)).Should().Be("uniplus.selecao.motivo_de_decisao.fase_ou_etapa_com_motivo_publicado");
    }

    private Task<HttpResponseMessage> SubstituirEtapaAsync(Guid processoId, Guid? id, string? etag = null, string? chave = null) =>
        _chamadas.EnviarAsync(
            HttpMethod.Put,
            $"/api/selecao/processos-seletivos/{processoId}/etapas",
            new[]
            {
                new
                {
                    id, nome = "Prova Objetiva", carater = 1, tipoEtapaOrigemId = TipoProvaObjetiva, peso = 1m, notaMinima = (decimal?)null, notaMaxima = 100m,
                    formaDeAvaliacao = "OBJETIVA", ordem = 1, faseCodigo = "RESULTADO_FINAL",
                    produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>(),
                },
            },
            etag,
            chave);
}
