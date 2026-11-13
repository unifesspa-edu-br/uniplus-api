namespace Unifesspa.UniPlus.Host.IntegrationTests.Providencias;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A precedência da providência com transações reais (UNI-REQ-0376, ADR-0164): vence quem grava primeiro. O teste segura
/// a linha disputada numa transação própria e só a solta depois de o banco registrar as requisições bloqueadas, de modo
/// que a ordem das travas é a do banco, e não a do relógio.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConcorrenciaDaProvidenciaNoHostTests : ProvidenciasNoHostTestBase
{
    public ConcorrenciaDaProvidenciaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A revogação por outro caminho confirmada antes da resposta faz a resposta ser recusada, e a providência aparece prejudicada")]
    public async Task RevogacaoExternaAntesDaResposta_RespostaRecusadaEPrejudicada()
    {
        Governanca g = await GovernancaAsync();
        Guid concessao = await ConcessaoDeLeandroAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, concessao);

        HttpResponseMessage[] respostas = await EmFilaSobATravaDaLinhaAsync(
            TravaDaConcessao, concessao, () => RevogarAsync(g.Diogo, concessao, "Revogada pela revogação comum.", Leandro.Email), () => ResponderAsync(g.Isabella, providencia));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.NoContent, await respostas[0].Content.ReadAsStringAsync());
        await ConferirCodigoAsync(respostas[1], HttpStatusCode.Conflict, "uniplus.autorizacao.providencia.concessao_nao_ativa");
        JsonElement lida = await ProvidenciaAsync(g.Diogo, providencia);
        (lida.GetProperty("situacao").GetString(), lida.GetProperty("encerramentoDaConcessao").GetProperty("causa").GetString()).Should().Be(("prejudicada", "revogada"));
        (await LinhaAsync(providencia)).Estado.Should().Be("aberta");
    }

    [Fact(DisplayName = "Duas respostas simultâneas: uma grava, a outra recebe o conflito de concorrência retentável, e só um aviso sai")]
    public async Task DuasRespostasSimultaneas_UmaGravaAOutraConflita()
    {
        Governanca g = await GovernancaAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, await ConcessaoDeLeandroAsync());

        HttpResponseMessage[] respostas = await EmFilaSobATravaDaLinhaAsync(
            TravaDaProvidencia, providencia, () => ResponderAsync(g.Diogo, providencia, "Motivo de Diogo."), () => ResponderAsync(g.Isabella, providencia, "Motivo de Isabella."));

        respostas.Select(static r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        HttpResponseMessage gravada = respostas.Single(static r => r.StatusCode == HttpStatusCode.OK);
        string conflito = await respostas.Single(static r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync();
        conflito.Should().Contain("uniplus.concorrencia.conflito").And.Contain("\"retryable\":true");
        (await LinhaAsync(providencia)).Motivo.Should().Be((await CorpoAsync(gravada, HttpStatusCode.OK)).GetProperty("desfecho").GetProperty("motivo").GetString());
        (await AvisosEmitidosAsync(providencia, "providencia_respondida")).Should().Be(1);
    }

    [Fact(DisplayName = "A resposta que chega depois da primeira confirmada é recusada por providência já respondida")]
    public async Task SegundaRespostaDepoisDaPrimeira_JaRespondida()
    {
        Governanca g = await GovernancaAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, await ConcessaoDeLeandroAsync());

        (await ResponderAsync(g.Diogo, providencia)).StatusCode.Should().Be(HttpStatusCode.OK);

        await ConferirCodigoAsync(await ResponderAsync(g.Isabella, providencia), HttpStatusCode.Conflict, "uniplus.autorizacao.providencia.ja_respondida");
    }

    [Fact(DisplayName = "A revogação vinculada repetida com a mesma chave devolve o mesmo resultado, sem revogar nem avisar de novo")]
    public async Task AtendimentoRepetidoComAMesmaChave_MesmoResultado()
    {
        Governanca g = await GovernancaAsync();
        Guid concessao = await ConcessaoDeLeandroAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, concessao);
        string chave = Guid.NewGuid().ToString();

        using HttpResponseMessage primeira = await AtenderAsync(g.Diogo, providencia, Leandro.Email, chave);
        using HttpResponseMessage repetida = await AtenderAsync(g.Diogo, providencia, Leandro.Email, chave);

        (await repetida.Content.ReadAsStringAsync()).Should().Be(await primeira.Content.ReadAsStringAsync());
        (primeira.StatusCode, repetida.StatusCode, repetida.Headers.GetValues("Idempotency-Replayed").Single()).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK, "true"));
        (await TiposDaTrilhaDaConcessaoAsync(g.Diogo, concessao)).Count(static t => t is "revogada" or "providencia_atendida").Should().Be(2);
        (await AvisosEmitidosAsync(providencia, "providencia_atendida"), await AvisosEmitidosAsync(providencia, "concessao_revogada_no_atendimento")).Should().Be((1, 1));
        await ConferirCodigoAsync(await AtenderAsync(g.Diogo, providencia, Leandro.Email), HttpStatusCode.Conflict, "uniplus.autorizacao.providencia.ja_atendida");
    }

    [Fact(DisplayName = "O atendimento sobre a concessão de um dos dois únicos gestores é recusado pelo mínimo de gestores, e nada muda")]
    public async Task AtendimentoDoPenultimoGestor_MinimoDeGestores()
    {
        await ResetarAsync();
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString(), TipoDeGovernanca.EncarregadoDeDados, Cecilia.Email);
        using HttpClient isabella = await ClienteComPermissoesAsync(Isabella, Administrar);
        Guid concessaoDeDiogo = await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Diogo.Sub.ToString(), TipoDeGovernanca.GestorDeAcessos, Diogo.Email);
        Guid providencia = await SolicitarAsync(Cliente(Cecilia), concessaoDeDiogo);

        await ConferirCodigoAsync(await AtenderAsync(isabella, providencia, Diogo.Email), HttpStatusCode.UnprocessableEntity, "uniplus.autorizacao.concessao.minimo_de_gestores");

        (await LinhaAsync(providencia)).Estado.Should().Be("aberta");
        (await AvisosEmitidosAsync(providencia, "providencia_atendida")).Should().Be(0);
        (await LerAsync(isabella, $"/api/autorizacao/concessoes/{concessaoDeDiogo}")).GetProperty("ativa").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "O atendimento com e-mail divergente ou ausente é recusado como a revogação comum, e o e-mail certo com outra caixa atende")]
    public async Task AtendimentoComEmailDivergente_RecusadoComoARevogacaoComum()
    {
        Governanca g = await GovernancaAsync();
        Guid concessao = await ConcessaoDeLeandroAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, concessao);

        JsonElement divergente = await CorpoAsync(await AtenderAsync(g.Diogo, providencia, "outra.pessoa@uniplus-test.local"), HttpStatusCode.UnprocessableEntity);
        JsonElement comum = await CorpoAsync(await RevogarAsync(g.Diogo, concessao, Motivo, "outra.pessoa@uniplus-test.local"), HttpStatusCode.UnprocessableEntity);
        (divergente.GetProperty("title").GetString(), divergente.GetProperty("errors")[0].GetProperty("field").GetString()).Should().Be((comum.GetProperty("title").GetString(), "confirmacaoEmail"));
        await ConferirCodigoAsync(await AtenderAsync(g.Diogo, providencia, null), HttpStatusCode.UnprocessableEntity, "uniplus.autorizacao.concessao.confirmacao_divergente");
        (await AvisosEmitidosAsync(providencia, "providencia_atendida")).Should().Be(0);

        (await AtenderAsync(g.Diogo, providencia, $"  {Leandro.Email.ToUpperInvariant()} ")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Duas aberturas simultâneas para a mesma concessão: uma abre, a outra recebe a recusa de já haver aberta")]
    public async Task DuasAberturasSimultaneas_UmaAbre()
    {
        Governanca g = await GovernancaAsync();
        Guid concessao = await ConcessaoDeLeandroAsync();

        HttpResponseMessage[] respostas = await EmFilaSobATravaDaLinhaAsync(
            TravaDaConcessao, concessao, () => SolicitarBrutoAsync(g.Cecilia, concessao), () => SolicitarBrutoAsync(g.Cecilia, concessao, "revisao"));

        respostas.Select(static r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        (await respostas.Single(static r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync())
            .Should().Contain("uniplus.autorizacao.providencia.ja_ha_aberta");
        (await FilaDasProvidenciasAsync(g.Diogo, $"?concessao={concessao}")).Should().ContainSingle();
    }

    private static async Task<string[]> TiposDaTrilhaDaConcessaoAsync(HttpClient cliente, Guid concessaoId) =>
        [.. (await LerAsync(cliente, $"/api/autorizacao/concessoes/{concessaoId}/trilha")).EnumerateArray().Select(static e => e.GetProperty("tipo").GetString()!)];
}
