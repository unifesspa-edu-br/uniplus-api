namespace Unifesspa.UniPlus.Host.IntegrationTests.Providencias;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

/// <summary>
/// O conflito de concorrência na gravação do atendimento: o endpoint tem chave de idempotência, então o conflito é
/// capturado e descartado no handler (ADR-0119); a revogação e o desfecho não ficam gravados, e a mesma chave, repetida
/// depois, executa de novo e atende.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConflitoNoAtendimentoNoHostTests : ProvidenciasNoHostTestBase, IClassFixture<HostComConflitoNaGravacao>
{
    private readonly HostComConflitoNaGravacao _host;

    public ConflitoNoAtendimentoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostComConflitoNaGravacao host)
        : base(fixture)
    {
        _host = host;
    }

    [Fact(DisplayName = "O conflito na gravação do atendimento responde 409 retentável, nada fica gravado nem avisado, e a mesma chave depois atende")]
    public async Task ConflitoNaGravacao_DesfazTudoELiberaAChave()
    {
        Governanca g = await GovernancaAsync();
        Guid concessao = await ConcessaoDeLeandroAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, concessao);
        string chave = Guid.NewGuid().ToString();
        _host.Conflito.Armar("UPDATE autorizacao.providencias SET updated_at = now() WHERE id = {0}", providencia);

        await ConferirCodigoAsync(await AtenderAsync(g.Diogo, providencia, Leandro.Email, chave), HttpStatusCode.Conflict, "uniplus.concorrencia.conflito");

        (await LerAsync(g.Diogo, $"/api/autorizacao/concessoes/{concessao}")).GetProperty("ativa").GetBoolean().Should().BeTrue("a revogação é desfeita com o desfecho");
        ((await LinhaAsync(providencia)).Estado, await AvisosEmitidosAsync(providencia, "concessao_revogada_no_atendimento")).Should().Be(("aberta", 0));
        (await AtenderAsync(g.Diogo, providencia, Leandro.Email, chave)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private protected override HttpClient NovoCliente() => _host.Obter(Fixture).CreateClient();
}
