namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using System.Net;
using System.Web;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// O cursor emitido pela listagem de campi, com o TTL configurado com um resto de ticks e o relógio com a 7ª casa decimal,
/// expira num instante em microssegundos inteiros: vale até um microssegundo antes e está expirado no instante exato.
/// </summary>
[Collection(ColecaoConfiguracaoComTempoEmResiduo.Name)]
[Trait("Category", "Integration")]
public sealed class ExpiracaoDoCursorComResiduoEndpointTests
{
    private readonly ConfiguracaoComTempoEmResiduoFixture _fixture;

    public ExpiracaoDoCursorComResiduoEndpointTests(ConfiguracaoComTempoEmResiduoFixture fixture) => _fixture = fixture;

    private ConfiguracaoComTempoEmResiduoFactory Fabrica => _fixture.Factory;

    private static DateTimeOffset InstanteComSetimaCasa()
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        return new DateTimeOffset(agora.UtcTicks - (agora.UtcTicks % TimeSpan.TicksPerSecond) + 1_234_567, TimeSpan.Zero);
    }

    private static DateTimeOffset ExpiracaoEsperada(DateTimeOffset emissao) =>
        new(emissao.UtcTicks - (emissao.UtcTicks % TimeSpan.TicksPerMicrosecond) + (15 * TimeSpan.TicksPerMinute), TimeSpan.Zero);

    private async Task<string> EmitirProximaPaginaAsync(DateTimeOffset emissao)
    {
        Fabrica.Relogio.ParaNo(emissao);
        using HttpClient client = Fabrica.CreateClient();
        foreach (string sigla in new[] { CampusHttpDeTeste.NovaSigla(), CampusHttpDeTeste.NovaSigla() })
        {
            using HttpResponseMessage criacao = await CampusHttpDeTeste.CriarAsync(client, sigla, Guid.NewGuid().ToString());
            criacao.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using HttpResponseMessage pagina = await client.GetAsync(new Uri("/api/configuracao/campi?limit=1", UriKind.Relative));
        return CampusHttpDeTeste.LinkRel(pagina, "next").Should().NotBeNull().And.Subject.As<string>();
    }

    private async Task<HttpStatusCode> ReapresentarAsync(string link, DateTimeOffset instante)
    {
        Fabrica.Relogio.ParaNo(instante);
        using HttpClient client = Fabrica.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(link, UriKind.RelativeOrAbsolute));
        return resposta.StatusCode;
    }

    [Fact(DisplayName = "O cursor emitido com TTL com resto de ticks guarda a expiração em microssegundos inteiros")]
    public async Task Emissao_TtlComResto_GuardaAExpiracaoEmMicrossegundos()
    {
        DateTimeOffset emissao = InstanteComSetimaCasa();
        string link = await EmitirProximaPaginaAsync(emissao);
        string cursor = HttpUtility.ParseQueryString(new Uri(new Uri("http://localhost"), link).Query)["cursor"]!;

        await using AsyncServiceScope escopo = Fabrica.Services.CreateAsyncScope();
        CursorDecodeResult decodificado = await escopo.ServiceProvider.GetRequiredService<CursorEncoder>().TryDecodeAsync(cursor);

        decodificado.Payload!.ExpiresAt.Should().Be(ExpiracaoEsperada(emissao));
    }

    [Fact(DisplayName = "Um microssegundo antes da expiração o cursor ainda leva à próxima página")]
    public async Task Reapresentacao_UmMicrossegundoAntesDaExpiracao_Vale()
    {
        DateTimeOffset emissao = InstanteComSetimaCasa();
        string link = await EmitirProximaPaginaAsync(emissao);

        HttpStatusCode status = await ReapresentarAsync(link, ExpiracaoEsperada(emissao).AddTicks(-10));

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "No instante exato da expiração o cursor está expirado e a listagem responde 410")]
    public async Task Reapresentacao_NoInstanteExatoDaExpiracao_Responde410()
    {
        DateTimeOffset emissao = InstanteComSetimaCasa();
        string link = await EmitirProximaPaginaAsync(emissao);

        HttpStatusCode status = await ReapresentarAsync(link, ExpiracaoEsperada(emissao));

        status.Should().Be(HttpStatusCode.Gone);
    }
}
