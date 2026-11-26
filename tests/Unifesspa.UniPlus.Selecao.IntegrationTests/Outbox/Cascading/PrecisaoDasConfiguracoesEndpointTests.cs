namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static PassosDoEnvelopeFechado;

[Collection(CascadingCollection.Name)]
public sealed class PrecisaoDasConfiguracoesEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task Fada_CasasExcedidas_AcumulaAsRecusas()
    {
        Cenario c = await PrepararCenarioAsync();
        Guid baseLegal = await SemearBaseLegalAsync(c.Api);
        HttpResponseMessage resposta = await c.Ctx.EnviarAsync(HttpMethod.Put, Rota(c, "fada"),
            Fada(baseLegal, 1.23456m, 2.34567m));
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("fator").And.Contain("teto");
        corpo.Split("uniplus.validacao.formato", StringSplitOptions.None).Length.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Fada_ValoresValidos_VoltaComOsMesmosNumeros()
    {
        Cenario c = await PrepararCenarioAsync();
        Guid baseLegal = await SemearBaseLegalAsync(c.Api);
        HttpResponseMessage resposta = await c.Ctx.EnviarAsync(HttpMethod.Put, Rota(c, "fada"), Fada(baseLegal, 1.25m, 95.5m));
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        ConfiguracaoDoFadaDto fada = (await c.Ctx.ObterProcessoAsync()).Fada!;
        fada.Fator.Should().Be(1.25m);
        fada.Teto.Should().Be(95.5m);
    }

    [Fact]
    public async Task Distribuicao_CasasExcedidas_AcumulaAsRecusas()
    {
        Cenario c = await PrepararCenarioAsync();
        HttpResponseMessage resposta = await c.Ctx.EnviarAsync(HttpMethod.Put, Rota(c, "distribuicao-vagas"),
            new[] { Distribuicao(c, 0, 0.75001m) });
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.validacao.formato").And.Contain("vagasOfertadasBase");
    }

    [Fact]
    public async Task Distribuicao_ValorValido_VoltaComOMesmoNumero()
    {
        Cenario c = await PrepararCenarioAsync();
        HttpResponseMessage resposta = await c.Ctx.EnviarAsync(HttpMethod.Put, Rota(c, "distribuicao-vagas"),
            new[] { Distribuicao(c, 60, 0.75m) });
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        ConfiguracaoDistribuicaoVagasDto[] gravada = (await resposta.Content.ReadFromJsonAsync<ConfiguracaoDistribuicaoVagasDto[]>())!;
        decimal? consultada = (await c.Ctx.ObterProcessoAsync()).DistribuicaoVagas.Single().PercentualDaReservaLegal;
        gravada.Single().PercentualDaReservaLegal.Should().Be(consultada).And.Be(0.75m);
    }

    private static async Task<Guid> SemearBaseLegalAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        BaseLegalDoFada baseLegal = BaseLegalDoFada.Criar(
            "PORTARIA", $"Portaria {Guid.CreateVersion7():N}", "Inclusão regional", [("1504208", "Marabá", "PA")]).Value!;
        db.Set<BaseLegalDoFada>().Add(baseLegal);
        await db.SaveChangesAsync();
        return baseLegal.Id;
    }

    private async Task<Cenario> PrepararCenarioAsync()
    {
        Cenario c = await PrepararAsync(fixture.Factory);
        await CriarProcessoEmRascunhoAsync(c);
        return c;
    }

    private static string Rota(Cenario c, string parte) => $"{ContextoDoEnvelopeFechado.Rota}/{c.Ctx.ProcessoId}/{parte}";

    private static object Fada(Guid baseLegal, decimal fator, decimal teto) => new
    {
        aplica = true,
        regra = new { codigo = RegraDoFada.Multiplicativo.Codigo(), versao = "v1" },
        fator,
        teto,
        baseLegal = new { id = baseLegal },
    };

    private static object Distribuicao(Cenario c, int vagas, decimal percentual) => new
    {
        ofertaCursoId = c.Ctx.Catalogos.OfertaCursoId,
        vagasOfertadasBase = vagas,
        percentualDaReservaLegal = percentual,
        regraDistribuicaoCodigo = RegraDeDistribuicaoDeVagas.Lei12711.Codigo(),
        regraDistribuicaoVersao = "v1",
        regraAjusteCodigo = RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11.Codigo(),
        regraAjusteVersao = "v1",
        referenciaReservaDemograficaId = c.Ctx.Catalogos.ReferenciaReservaDemograficaId,
        modalidadeIds = c.Ctx.Catalogos.ModalidadeIdPorCodigo.Values.ToArray(),
        quadro = Array.Empty<object>(),
    };
}
