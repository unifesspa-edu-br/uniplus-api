namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Outbox.Cascading;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;
using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// Importações do mesmo escopo, com o relógio parado no mesmo microssegundo e duas delas concorrentes: a trava do
/// processo as serializa, e cada uma grava o seu lote, com o mesmo instante e a sua própria ordem de registro.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ImportacoesNoMesmoInstanteTests : IDisposable
{
    private static readonly DateTimeOffset FimDasInscricoes = new(2099, 12, 20, 15, 0, 0, TimeSpan.Zero);

    private readonly CascadingFixture _fixture;
    private readonly RelogioControlado _fonte = new();
    private readonly WebApplicationFactory<HostAssemblyMarker> _host;

    public ImportacoesNoMesmoInstanteTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _host = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(new RelogioEmMicrossegundos(_fonte));
        }));
    }

    [Fact(DisplayName = "Importações concorrentes no mesmo microssegundo são serializadas pela trava e gravam um lote cada")]
    public async Task ImportacoesConcorrentesNoMesmoInstante_SerializadasUmLoteCada()
    {
        Publicado processo = await SemearEPublicarAsync(_fixture, FimDasInscricoes);
        _fonte.ParaNo(FimDasInscricoes.AddDays(1).AddTicks(1_234_567));
        using HttpClient client = _host.CreateClient();

        // A primeira vai sozinha: é ela que concede ao administrador de teste o acesso que as concorrentes usam.
        HttpResponseMessage primeira = await ImportarAsync(client, processo, 0);
        HttpResponseMessage[] concorrentes = await Task.WhenAll(ImportarAsync(client, processo, 1), ImportarAsync(client, processo, 2));

        concorrentes.Append(primeira).Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.Created);
        LoteGravado[] lotes = await LotesAsync(processo);
        lotes.Should().HaveCount(3);
        lotes.Select(l => l.ImportadoEm).Distinct().Should().ContainSingle();
        lotes.Select(l => l.OrdemDeRegistro).Should().OnlyHaveUniqueItems();
    }

    private static Task<HttpResponseMessage> ImportarAsync(HttpClient client, Publicado processo, int pessoa) =>
        ImportacaoDeNotasDeTeste.ImportarAsync(client, processo, Bytes([Linha(PersonasDeTeste.Candidatas[pessoa], pessoa + 1)]));

    public void Dispose() => _host.Dispose();

    private async Task<LoteGravado[]> LotesAsync(Publicado processo)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().LotesDeNotasDoEnem.AsNoTracking()
            .Where(l => l.ProcessoSeletivoId == processo.ProcessoId)
            .Select(l => new LoteGravado(l.ImportadoEm, EF.Property<long>(l, LoteDeNotasDoEnemConfiguration.OrdemDeRegistro)))
            .ToArrayAsync();
    }

    private sealed record LoteGravado(DateTimeOffset ImportadoEm, long OrdemDeRegistro);
}
