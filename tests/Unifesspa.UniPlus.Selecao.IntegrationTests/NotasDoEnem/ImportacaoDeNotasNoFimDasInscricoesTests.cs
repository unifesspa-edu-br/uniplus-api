namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Outbox.Cascading;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;
using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// A importação das notas do ENEM só vale depois do encerramento das inscrições, e o último instante da janela ainda é
/// inscrição aberta. O relógio de produção corta a 7ª casa: a fonte que está poucos ticks além do fim ainda está, para o
/// sistema, no último instante da janela, o mesmo instante que o banco guardaria.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ImportacaoDeNotasNoFimDasInscricoesTests : IDisposable
{
    private static readonly DateTimeOffset FimDasInscricoes = new(2099, 12, 20, 15, 0, 0, TimeSpan.Zero);

    private readonly CascadingFixture _fixture;
    private readonly RelogioControlado _fonte = new();
    private readonly WebApplicationFactory<HostAssemblyMarker> _host;

    public ImportacaoDeNotasNoFimDasInscricoesTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _host = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(new RelogioEmMicrossegundos(_fonte));
        }));
    }

    [Fact(DisplayName = "Com a fonte quatro ticks além do fim das inscrições, a importação ainda é recusada: é o último instante da janela")]
    public async Task ImportarNoUltimoInstante_ComResiduoNaFonte_Recusa()
    {
        Publicado processo = await SemearEPublicarAsync(_fixture, FimDasInscricoes);
        _fonte.ParaNo(FimDasInscricoes.AddTicks(4));

        using HttpResponseMessage resposta = await ImportarAsync(processo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.notas_do_enem.inscricoes_em_andamento");
    }

    [Fact(DisplayName = "Um microssegundo depois do fim das inscrições, a importação é aceita")]
    public async Task ImportarUmMicrossegundoDepois_Aceita()
    {
        Publicado processo = await SemearEPublicarAsync(_fixture, FimDasInscricoes);
        _fonte.ParaNo(FimDasInscricoes.AddTicks(TimeSpan.TicksPerMicrosecond));

        using HttpResponseMessage resposta = await ImportarAsync(processo);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    public void Dispose() => _host.Dispose();

    private async Task<HttpResponseMessage> ImportarAsync(Publicado processo)
    {
        using HttpClient client = _host.CreateClient();
        return await ImportacaoDeNotasDeTeste.ImportarAsync(client, processo, Bytes([Linha(PersonasDeTeste.Candidatas[0], 1)]));
    }
}
