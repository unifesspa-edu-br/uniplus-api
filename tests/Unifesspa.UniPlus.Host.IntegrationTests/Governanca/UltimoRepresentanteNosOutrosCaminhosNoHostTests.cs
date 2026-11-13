namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A regra do último Representante do controlador vale também no atendimento da providência e na revogação do item da
/// revisão de acessos, que passam pela mesma regra comum sob a trava do papel (UNI-REQ-0394, UNI-REQ-0376): a recusa não
/// grava o desfecho da providência nem a decisão do item. Entre caminhos diferentes, as revogações simultâneas também se
/// serializam pela trava do papel.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class UltimoRepresentanteNosOutrosCaminhosNoHostTests : UltimoRepresentanteNoHostTestBase
{
    private const string RotaDasRevisoes = "/api/autorizacao/revisoes";

    public UltimoRepresentanteNosOutrosCaminhosNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O atendimento da providência sobre a única designação de Representante é recusado, e a providência segue aberta")]
    public async Task Atender_UnicaDesignacao_Recusa()
    {
        Governanca g = await GovernancaAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, await DesignacaoAsync(Marlene));

        await ConferirCodigoAsync(await AtenderAsync(g.Diogo, providencia, Marlene.Email), HttpStatusCode.UnprocessableEntity, UltimoRepresentante);

        (await LinhaAsync(providencia)).Estado.Should().Be("aberta");
        (await VigentesAsync()).Should().Equal(Marlene.Sub.ToString());
    }

    [Fact(DisplayName = "A revogação do item da revisão sobre a única designação de Representante é recusada, e o item segue pendente")]
    public async Task RevogarItem_UnicaDesignacao_Recusa()
    {
        Governanca g = await GovernancaAsync();
        (Guid revisaoId, Guid itemId) = await RevisaoComOItemDeMarleneAsync(g.Marlene);

        await ConferirCodigoAsync(
            await EnviarAsync(g.Diogo, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{itemId}/revogacao", new { motivo = Motivo }),
            HttpStatusCode.UnprocessableEntity,
            UltimoRepresentante);

        ItemDe(await LerAsync(g.Marlene, $"{RotaDasRevisoes}/{revisaoId}"), itemId).GetProperty("situacao").GetString().Should().Be("pendente");
        (await VigentesAsync()).Should().Equal(Marlene.Sub.ToString());
    }

    [Fact(DisplayName = "A revogação direta e o atendimento simultâneos dos dois últimos Representantes: um passa, o outro é recusado")]
    public async Task RevogacaoEAtendimentoSimultaneos_SobraExatamenteUm()
    {
        Governanca g = await GovernancaAsync();
        Guid marlene = await DesignacaoAsync(Marlene);
        Guid providencia = await SolicitarAsync(g.Cecilia, await DesignacaoAsync(Leandro));

        HttpResponseMessage[] respostas = await EmFilaSobATravaDaLinhaAsync(
            TravaDoPapel, Representante, () => RevogarAsync(g.Diogo, marlene, Motivo, Marlene.Email), () => AtenderAsync(g.Isabella, providencia, Leandro.Email));

        HttpStatusCode aceita = respostas[0].StatusCode == HttpStatusCode.NoContent ? HttpStatusCode.NoContent : HttpStatusCode.OK;
        (await VigentesAsync()).Should().ContainSingle("a instituição não pode ficar sem Representante vigente");
        await ConferirUmaAceitaEOutraRecusadaAsync(respostas, aceita);
    }

    // Marlene abre a revisão; o item da designação dela cabe aos gestores, porque ela é Representante.
    private static async Task<(Guid RevisaoId, Guid ItemId)> RevisaoComOItemDeMarleneAsync(HttpClient marlene)
    {
        JsonElement revisao = await CorpoAsync(await EnviarAsync(marlene, HttpMethod.Post, RotaDasRevisoes), HttpStatusCode.Created);
        JsonElement item = revisao.GetProperty("itens").EnumerateArray()
            .Single(i => i.GetProperty("beneficiario").GetProperty("sub").GetString() == Marlene.Sub.ToString());

        return (revisao.GetProperty("id").GetGuid(), item.GetProperty("id").GetGuid());
    }

    private static JsonElement ItemDe(JsonElement revisao, Guid itemId) =>
        revisao.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == itemId);
}
