namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static PassosDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

/// <summary>
/// A quantidade de cada linha do quadro de vagas é declarada pelo pedido: a falta recusa com 422 no campo, uma vez só, no PUT e na
/// simulação, e nada é gravado. O corpo é uma lista de distribuições, e o caminho do erro traz a posição da distribuição
/// (<c>distribuicaoVagas[0]</c>) antes do campo do quadro.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class QuantidadeDoQuadroDeVagasEndpointTests(CascadingFixture fixture)
{
    private const string MediaTypeDaSimulacao = "application/vnd.uniplus.simulacao-distribuicao-vagas.v1+json";

    [Theory(DisplayName = "A quantidade omitida ou nula na linha do quadro é recusada no campo, uma vez só, e nada é gravado")]
    [InlineData("omitida", false)]
    [InlineData("nula", false)]
    [InlineData("omitida", true)]
    [InlineData("nula", true)]
    public async Task QuantidadeAusente_RecusaUmaVezNoCampo(string forma, bool simulacao)
    {
        Cenario c = await PrepararCenarioAsync();
        JsonObject linha = new() { ["modalidadeId"] = c.Ctx.Catalogos.ModalidadeIdPorCodigo.Values.First() };
        if (forma == "nula")
        {
            linha["quantidade"] = null;
        }

        using HttpResponseMessage resposta = await EnviarAsync(c, linha, simulacao);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        ErroDoProblema[] erros = await LerErrosDoProblemaAsync(resposta);
        erros.Should().ContainSingle().Which.Should().Be(new ErroDoProblema("distribuicaoVagas[0].quadro[0].quantidade", "uniplus.validacao.obrigatorio"));
        (await c.Ctx.ObterProcessoAsync()).DistribuicaoVagas.Should().BeEmpty();
    }

    private async Task<Cenario> PrepararCenarioAsync()
    {
        Cenario c = await PrepararAsync(fixture.Factory);
        await CriarProcessoEmRascunhoAsync(c);
        return c;
    }

    private static object Distribuicao(Cenario c, JsonObject linhaDoQuadro) => new
    {
        ofertaCursoId = c.Ctx.Catalogos.OfertaCursoId,
        vagasOfertadasBase = 60,
        percentualDaReservaLegal = 0.75m,
        regraDistribuicaoCodigo = RegraDeDistribuicaoDeVagas.Lei12711.Codigo(),
        regraDistribuicaoVersao = "v1",
        regraAjusteCodigo = RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11.Codigo(),
        regraAjusteVersao = "v1",
        referenciaReservaDemograficaId = c.Ctx.Catalogos.ReferenciaReservaDemograficaId,
        modalidadeIds = c.Ctx.Catalogos.ModalidadeIdPorCodigo.Values.ToArray(),
        quadro = new[] { linhaDoQuadro },
    };

    private static Task<HttpResponseMessage> EnviarAsync(Cenario c, JsonObject linhaDoQuadro, bool simulacao)
    {
        object[] corpo = [Distribuicao(c, linhaDoQuadro)];
        string rota = $"{ContextoDoEnvelopeFechado.Rota}/{c.Ctx.ProcessoId}/distribuicao-vagas";

        return simulacao
            ? c.Ctx.EnviarAsync(HttpMethod.Post, $"{rota}/simulacao", corpo, idempotente: false, accept: MediaTypeDaSimulacao)
            : c.Ctx.EnviarAsync(HttpMethod.Put, rota, corpo);
    }
}
