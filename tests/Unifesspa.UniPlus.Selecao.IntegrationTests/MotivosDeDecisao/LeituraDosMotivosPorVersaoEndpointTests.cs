namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao.ChamadasDosMotivosDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CicloDaRetificacaoApoio;

/// <summary>
/// A leitura dos motivos do processo numa versão publicada do edital pelo endpoint real, contra Postgres via Testcontainers
/// (UNI-REQ-0124, UNI-REQ-0285): a versão 1 só traz o motivo escolhido antes da publicação, a versão 2 traz também o
/// incluído na retificação, e o motivo que saiu vale até a versão anterior à saída.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class LeituraDosMotivosPorVersaoEndpointTests : IAsyncLifetime
{
    private readonly CascadingFixture _fixture;
    private readonly ChamadasDosMotivosDeTeste _chamadas;

    public LeituraDosMotivosPorVersaoEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
        _chamadas = new ChamadasDosMotivosDeTeste(fixture, "TEST_LMV");
    }

    public Task InitializeAsync() => TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);

    public Task DisposeAsync() => _chamadas.LimparAsync();

    [Fact(DisplayName = "A versão 1 traz o motivo A, a versão 2 traz A e B, e a versão que o processo não publicou devolve versão não publicada")]
    public async Task Leitura_PorVersao_TrazOQueValiaNela()
    {
        (Guid processoId, MotivoDeDecisao a, MotivoDeDecisao b) = await PublicarComAERetificarComBAsync(nameof(Leitura_PorVersao_TrazOQueValiaNela));

        Codigos(await _chamadas.LerAsync(processoId, versao: 1)).Should().Equal(a.Codigo.Valor);
        Codigos(await _chamadas.LerAsync(processoId, versao: 2)).Should().BeEquivalentTo([a.Codigo.Valor, b.Codigo.Valor]);
        Codigos(await _chamadas.LerAsync(processoId)).Should().BeEquivalentTo([a.Codigo.Valor, b.Codigo.Valor]);
        using HttpResponseMessage naoPublicada = await _chamadas.LerRespostaAsync(processoId, versao: 3);
        naoPublicada.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoDoErroAsync(naoPublicada)).Should().Be("uniplus.selecao.motivo_de_decisao.versao_nao_publicada");
    }

    [Fact(DisplayName = "O motivo com saída na versão 2 vale na versão 1 e não vale na 2 nem na escolha atual")]
    public async Task Leitura_MotivoComSaida_ValeAteAVersaoAnterior()
    {
        (Guid processoId, MotivoDeDecisao a, MotivoDeDecisao b) = await PublicarComAERetificarComBAsync(nameof(Leitura_MotivoComSaida_ValeAteAVersaoAnterior));
        await DefinirSaidaNaVersaoAsync(processoId, a.Id, versaoDeSaida: 2);

        Codigos(await _chamadas.LerAsync(processoId, versao: 1)).Should().Equal(a.Codigo.Valor);
        Codigos(await _chamadas.LerAsync(processoId, versao: 2)).Should().Equal(b.Codigo.Valor);
        Codigos(await _chamadas.LerAsync(processoId)).Should().Equal(b.Codigo.Valor);
    }

    private static string?[] Codigos(IReadOnlyList<JsonElement> escolhidos) =>
        [.. escolhidos.Select(static e => e.GetProperty("codigo").GetString())];

    private async Task<(Guid ProcessoId, MotivoDeDecisao A, MotivoDeDecisao B)> PublicarComAERetificarComBAsync(string nome)
    {
        (Guid processoId, Guid documentoId) = await _chamadas.SemearProcessoAsync(nome);
        MotivoDeDecisao a = await _chamadas.SemearMotivoAsync();
        MotivoDeDecisao b = await _chamadas.SemearMotivoAsync();
        Guid fase = await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaFase(processoId, fase, a.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _chamadas.PublicarAsync(processoId, documentoId);
        await RetificarIncluindoNaEtapaAsync(processoId, b.Id);

        return (processoId, a, b);
    }

    private async Task RetificarIncluindoNaEtapaAsync(Guid processoId, Guid motivoId)
    {
        Guid etapa = await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId);
        using HttpClient client = _fixture.Factory.CreateClient();
        CicloDaRetificacao ciclo = new(_fixture.Factory, client, processoId);
        string etag = LerETag(await ciclo.AbrirAsync("Inclusão de motivo de decisão"));
        using HttpResponseMessage inclusao = await _chamadas.IncluirAsync(
            LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, etapa, motivoId), "Retorno do INEP passou a ser exigido pela norma.", etag);
        inclusao.StatusCode.Should().Be(HttpStatusCode.NoContent, await inclusao.Content.ReadAsStringAsync());
        using HttpResponseMessage fechamento = await ciclo.FecharAsync(await SemearDocumentoConfirmadoAsync(ciclo), LerETag(await ciclo.ObterSessaoAsync()));
        fechamento.StatusCode.Should().Be(HttpStatusCode.NoContent, await fechamento.Content.ReadAsStringAsync());
    }

    private async Task DefinirSaidaNaVersaoAsync(Guid processoId, Guid motivoOrigemId, int versaoDeSaida)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        int alteradas = await db.Database.ExecuteSqlAsync(
            $"UPDATE selecao.motivos_do_processo SET versao_do_edital_de_saida = {versaoDeSaida} WHERE processo_seletivo_id = {processoId} AND motivo_de_decisao_origem_id = {motivoOrigemId}");
        alteradas.Should().Be(1);
    }
}
