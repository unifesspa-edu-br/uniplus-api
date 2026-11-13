namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Providencias;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

/// <summary>
/// A tramitação da providência pelos endpoints reais (UNI-REQ-0375, UNI-REQ-0376): a solicitação pelo Encarregado de
/// dados, a fila, a resposta e o atendimento por quem administra concessões, e as recusas do contrato.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ProvidenciaEndpointTests : ProvidenciaEndpointTestBase
{
    public ProvidenciaEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O Encarregado solicita com fundamento, e a providência aberta entra na fila de quem administra concessões")]
    public async Task Solicitar_EntraNaFila()
    {
        Guid concessao = await ConcederAsync(await CriarPerfilComumAsync(), Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient cecilia = await ClienteDoEncarregadoAsync();

        using HttpResponseMessage resposta = await SolicitarAsync(cecilia, concessao);

        JsonElement corpo = await CorpoAsync(resposta, HttpStatusCode.Created);
        resposta.Headers.Location!.ToString().Should().EndWith($"/providencias/{corpo.GetProperty("id").GetGuid()}");
        (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("fundamento").GetString()).Should().Be(("aberta", Fundamento));
        (await FilaAsync(await ClienteDoGestorAsync(Diogo), $"?concessao={concessao}")).Should().Equal(corpo.GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "Tipo fora do vocabulário e fundamento ausente voltam juntos em errors[]")]
    public async Task Solicitar_Invalida_ErrosAcumulados()
    {
        Guid concessao = await ConcederAsync(await CriarPerfilComumAsync(), Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient cecilia = await ClienteDoEncarregadoAsync();

        JsonElement corpo = await CorpoAsync(await SolicitarAsync(cecilia, concessao, "suspensao", null), HttpStatusCode.UnprocessableEntity);

        corpo.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString()).Should().Equal("tipo", "fundamento");
    }

    [Fact(DisplayName = "A concessão inexistente responde 404, e a segunda providência aberta para a mesma concessão responde 409")]
    public async Task Solicitar_InexistenteEJaAberta()
    {
        (Guid concessao, _) = await ProvidenciaAbertaAsync();
        using HttpClient cecilia = await ClienteDoEncarregadoAsync();

        await ConferirRecusaAsync(await SolicitarAsync(cecilia, Guid.NewGuid()), HttpStatusCode.NotFound, "uniplus.autorizacao.concessao.nao_encontrada");
        await ConferirRecusaAsync(await SolicitarAsync(cecilia, concessao, "revisao"), HttpStatusCode.Conflict, "uniplus.autorizacao.providencia.ja_ha_aberta");
    }

    [Fact(DisplayName = "Quem administra concessões e não tem a permissão de solicitar não solicita, e o Encarregado não responde nem atende")]
    public async Task Permissoes_PorRota()
    {
        (Guid concessao, Guid providencia) = await ProvidenciaAbertaAsync();
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        using HttpClient cecilia = await ClienteDoEncarregadoAsync();

        (await SolicitarAsync(diogo, concessao, "revisao")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ResponderAsync(cecilia, providencia)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AtenderAsync(cecilia, providencia)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Cliente(Pessoa("caroline.jesus")).GetAsync(new Uri($"{RotaDasProvidencias}/{providencia}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "A resposta motivada leva a respondida, e a segunda resposta é recusada por já respondida")]
    public async Task Responder_DepoisJaRespondida()
    {
        (_, Guid providencia) = await ProvidenciaAbertaAsync();
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        (await CorpoAsync(await ResponderAsync(diogo, providencia), HttpStatusCode.OK)).GetProperty("situacao").GetString().Should().Be("respondida");

        await ConferirRecusaAsync(await ResponderAsync(await ClienteDoGestorAsync(Isabella), providencia), HttpStatusCode.Conflict, "uniplus.autorizacao.providencia.ja_respondida");
    }

    [Fact(DisplayName = "O motivo ausente é recusado no campo motivo, e o beneficiário não responde a providência sobre a própria concessão")]
    public async Task Responder_MotivoEBeneficiario()
    {
        (_, Guid providencia) = await ProvidenciaAbertaAsync();
        (_, Guid sobreDiogo) = await ProvidenciaAbertaAsync(Diogo);
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        await ConferirRecusaAsync(await ResponderAsync(diogo, providencia, " "), HttpStatusCode.UnprocessableEntity, "uniplus.autorizacao.providencia.motivo_invalido");
        await ConferirRecusaAsync(await ResponderAsync(diogo, sobreDiogo), HttpStatusCode.UnprocessableEntity, "uniplus.autorizacao.providencia.beneficiario_nao_responde");
    }

    [Fact(DisplayName = "O atendimento revoga a concessão, e a trilha dela mostra a solicitação, a revogação e o atendimento")]
    public async Task Atender_RevogaETrilha()
    {
        (Guid concessao, Guid providencia) = await ProvidenciaAbertaAsync();
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        JsonElement corpo = await CorpoAsync(await AtenderAsync(diogo, providencia), HttpStatusCode.OK);

        (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("concessao").GetProperty("ativa").GetBoolean()).Should().Be(("atendida", false));
        using JsonDocument trilha = await LerAsync(diogo, $"{Rota}/{concessao}/trilha");
        trilha.RootElement.EnumerateArray().Select(static e => e.GetProperty("tipo").GetString()).Should()
            .Contain(["providencia_solicitada", "revogada", "providencia_atendida"]);
    }

    [Fact(DisplayName = "O e-mail que não é o do beneficiário recusa o atendimento com o código da revogação comum, e o motivo acumula")]
    public async Task Atender_EmailDivergente()
    {
        (Guid concessao, Guid providencia) = await ProvidenciaAbertaAsync();
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        JsonElement corpo = await CorpoAsync(
            await EnviarAsync(diogo, HttpMethod.Post, $"{RotaDasProvidencias}/{providencia}/atendimento", new { motivo = "", confirmacaoEmail = "outra@uniplus-test.local" }),
            HttpStatusCode.UnprocessableEntity);

        corpo.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("code").GetString()).Should().Equal(
            "uniplus.autorizacao.concessao.confirmacao_divergente", "uniplus.autorizacao.providencia.motivo_invalido");
        using JsonDocument lida = await LerAsync(diogo, $"{Rota}/{concessao}");
        lida.RootElement.GetProperty("ativa").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "A revogação por outro caminho deixa a providência prejudicada: sai da fila padrão e entra no filtro de prejudicadas")]
    public async Task Prejudicada_PelaRevogacaoComum()
    {
        (Guid concessao, Guid providencia) = await ProvidenciaAbertaAsync();
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        (await RevogarAsync(diogo, concessao)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement lida = await CorpoAsync(await diogo.GetAsync(new Uri($"{RotaDasProvidencias}/{providencia}", UriKind.Relative)), HttpStatusCode.OK);

        (lida.GetProperty("situacao").GetString(), lida.GetProperty("encerramentoDaConcessao").GetProperty("causa").GetString()).Should().Be(("prejudicada", "revogada"));
        (await FilaAsync(diogo, $"?concessao={concessao}")).Should().BeEmpty();
        (await FilaAsync(diogo, $"?concessao={concessao}&situacao=prejudicada")).Should().Equal(providencia);
        await ConferirRecusaAsync(await ResponderAsync(diogo, providencia), HttpStatusCode.Conflict, "uniplus.autorizacao.providencia.concessao_nao_ativa");
    }

    [Fact(DisplayName = "A situação fora do vocabulário do filtro responde 400")]
    public async Task Fila_SituacaoInvalida()
    {
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri($"{RotaDasProvidencias}?situacao=pendente", UriKind.Relative));

        await ConferirRecusaAsync(resposta, HttpStatusCode.BadRequest, "uniplus.autorizacao.providencia.filtro_de_situacao_invalido");
    }
}
