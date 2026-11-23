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

/// <summary>
/// A leitura dos motivos do processo numa versão publicada do edital pelo endpoint real, contra Postgres via Testcontainers
/// (UNI-REQ-0124, UNI-REQ-0285): a versão 1 traz o motivo escolhido antes da publicação, a versão 2, nascida da retificação do ato, o
/// conserva, e o motivo que saiu vale até a versão anterior à saída.
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

    [Fact(DisplayName = "A versão 1 e a versão 2, nascida da retificação do ato, trazem o motivo escolhido antes da publicação, e a versão que o processo não publicou devolve versão não publicada")]
    public async Task Leitura_PorVersao_TrazOQueValiaNela()
    {
        (Guid processoId, MotivoDeDecisao a) = await PublicarERetificarOAtoAsync(nameof(Leitura_PorVersao_TrazOQueValiaNela));

        Codigos(await _chamadas.LerAsync(processoId, versao: 1)).Should().Equal(a.Codigo.Valor);
        Codigos(await _chamadas.LerAsync(processoId, versao: 2)).Should().Equal(a.Codigo.Valor);
        Codigos(await _chamadas.LerAsync(processoId)).Should().Equal(a.Codigo.Valor);
        using HttpResponseMessage naoPublicada = await _chamadas.LerRespostaAsync(processoId, versao: 3);
        naoPublicada.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoDoErroAsync(naoPublicada)).Should().Be("uniplus.selecao.motivo_de_decisao.versao_nao_publicada");
    }

    [Fact(DisplayName = "Retificado o ato depois de o cadastro desativar o motivo e mudar a descrição, a retificação é aceita e a versão 2 traz a cópia publicada")]
    public async Task Retificacao_MotivoDesativadoEEditadoNoCadastro_ConservaACopiaPublicada()
    {
        (Guid processoId, Guid documentoId) = await _chamadas.SemearProcessoAsync(nameof(Retificacao_MotivoDesativadoEEditadoNoCadastro_ConservaACopiaPublicada));
        MotivoDeDecisao a = await _chamadas.SemearMotivoAsync();
        Guid fase = await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaFase(processoId, fase, a.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _chamadas.PublicarAsync(processoId, documentoId);
        await DesativarEEditarNoCadastroAsync(a.Id, "Redação do cadastro depois da publicação");

        await RetificarOAtoAsync(processoId);

        JsonElement naVersao2 = (await _chamadas.LerAsync(processoId, versao: 2)).Should().ContainSingle().Subject;
        naVersao2.GetProperty("descricao").GetString().Should().Be(a.Descricao);
        naVersao2.GetProperty("codigo").GetString().Should().Be(a.Codigo.Valor);
    }

    [Fact(DisplayName = "Com a versão de saída gravada por SQL, estado que o código ainda não produz, a leitura por versão devolve o motivo na versão anterior à saída e não devolve na saída nem na escolha atual")]
    public async Task Leitura_VersaoDeSaidaGravadaPorSql_ValeAteAVersaoAnterior()
    {
        (Guid processoId, MotivoDeDecisao a) = await PublicarERetificarOAtoAsync(nameof(Leitura_VersaoDeSaidaGravadaPorSql_ValeAteAVersaoAnterior));
        await DefinirSaidaNaVersaoAsync(processoId, a.Id, versaoDeSaida: 2);

        Codigos(await _chamadas.LerAsync(processoId, versao: 1)).Should().Equal(a.Codigo.Valor);
        Codigos(await _chamadas.LerAsync(processoId, versao: 2)).Should().BeEmpty();
        Codigos(await _chamadas.LerAsync(processoId)).Should().BeEmpty();
    }

    private static string?[] Codigos(IReadOnlyList<JsonElement> escolhidos) =>
        [.. escolhidos.Select(static e => e.GetProperty("codigo").GetString())];

    private async Task<(Guid ProcessoId, MotivoDeDecisao A)> PublicarERetificarOAtoAsync(string nome)
    {
        (Guid processoId, Guid documentoId) = await _chamadas.SemearProcessoAsync(nome);
        MotivoDeDecisao a = await _chamadas.SemearMotivoAsync();
        Guid fase = await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaFase(processoId, fase, a.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _chamadas.PublicarAsync(processoId, documentoId);
        await RetificarOAtoAsync(processoId);

        return (processoId, a);
    }

    private async Task RetificarOAtoAsync(Guid processoId)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid documento = await PublicacaoPeloEndpointDeTeste.SemearDocumentoConfirmadoAsync(_fixture.Factory, processoId);
        using HttpResponseMessage retificacao = await PublicacaoPeloEndpointDeTeste.RetificarAsync(client, processoId, documento);
        retificacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await retificacao.Content.ReadAsStringAsync());
    }

    private async Task DesativarEEditarNoCadastroAsync(Guid motivoId, string descricao)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        MotivoDeDecisao motivo = await db.MotivosDeDecisao.SingleAsync(m => m.Id == motivoId);
        motivo.Atualizar(descricao).IsSuccess.Should().BeTrue();
        motivo.Desativar().IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task DefinirSaidaNaVersaoAsync(Guid processoId, Guid motivoOrigemId, int versaoDeSaida)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        int alteradas = await db.Database.ExecuteSqlAsync(
            $"UPDATE selecao.motivos_de_decisao_do_processo SET versao_do_edital_de_saida = {versaoDeSaida} WHERE processo_seletivo_id = {processoId} AND motivo_de_decisao_origem_id = {motivoOrigemId}");
        alteradas.Should().Be(1);
    }
}
