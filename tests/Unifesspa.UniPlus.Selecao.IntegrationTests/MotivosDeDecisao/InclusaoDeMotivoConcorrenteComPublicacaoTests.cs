namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Globalization;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A inclusão de motivo numa fase do processo e a publicação do mesmo processo disputando a linha dele (UNI-REQ-0285,
/// UNI-REQ-0019): as duas bloqueiam a linha do processo antes de ler qualquer coisa, então se serializam e terminam num de dois
/// desfechos, nunca num meio-termo. A inclusão que entra antes é conferida pela publicação e consta da primeira versão; a que
/// chega depois da publicação é recusada pelo bloqueio da mutação direta e não altera a versão já publicada.
/// </summary>
/// <remarks>
/// A disputa é montada com uma sessão do teste que retém a linha do processo: as duas operações reais entram na fila do banco,
/// uma de cada vez e em ordem conhecida, e só então a linha é liberada. O primeiro da fila vence, sem depender de tempo.
/// O resultado da publicação depende do motivo disputado: o processo exige motivo favorável e desfavorável para o fundamento
/// de isenção. Quando a inclusão vem primeiro, o motivo favorável só existe por causa dela, e a publicação só passa se a
/// conferir já gravado; sem isso recusaria por cobertura insuficiente. Quando a publicação vem primeiro, o processo já está
/// coberto sem o motivo disputado, a publicação vence e a inclusão é recusada.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class InclusaoDeMotivoConcorrenteComPublicacaoTests : IAsyncLifetime
{
    private static readonly TimeSpan EsperaDoBloqueio = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EsperaDoTeste = TimeSpan.FromSeconds(60);

    private readonly CascadingFixture _fixture;
    private readonly ChamadasDosMotivosDeTeste _chamadas;

    public InclusaoDeMotivoConcorrenteComPublicacaoTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
        _chamadas = new ChamadasDosMotivosDeTeste(fixture, "TEST_ICP");
    }

    public enum OrdemDeChegada
    {
        InclusaoPrimeiro,
        PublicacaoPrimeiro,
    }

    public Task InitializeAsync() => TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);

    public Task DisposeAsync() => _chamadas.LimparAsync();

    [Theory(DisplayName = "Inclusão de motivo e publicação disputando o processo se serializam: a publicação só vê o motivo incluído antes dela, e o incluído depois é recusado sem alterar a versão publicada")]
    [InlineData(OrdemDeChegada.InclusaoPrimeiro)]
    [InlineData(OrdemDeChegada.PublicacaoPrimeiro)]
    public async Task InclusaoDeMotivoConcorrenteComPublicacao_SeSerializaSemMeioTermo(OrdemDeChegada ordem)
    {
        Cenario cenario = await PrepararAsync(ordem);

        Desfecho desfecho = await DisputarAsync(cenario, ordem);

        ConferirResultadosDasOperacoes(desfecho, ordem);
        await ConferirEstadoFinalAsync(cenario, ordem);
    }

    /// <summary>
    /// Com a inclusão na frente, o processo só tem o motivo desfavorável e o favorável é o disputado: a cobertura depende dele.
    /// Com a publicação na frente, os dois sentidos já estão escolhidos e o disputado é um desfavorável a mais.
    /// </summary>
    private async Task<Cenario> PrepararAsync(OrdemDeChegada ordem)
    {
        (Guid processoId, Guid documentoId) = await ProcessoComIsencaoDeTeste.SemearAsync(_chamadas.Services, nameof(InclusaoDeMotivoConcorrenteComPublicacao_SeSerializaSemMeioTermo));
        Guid faseId = await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId, ProcessoComIsencaoDeTeste.CodigoDaFase);
        List<Guid> anteriores = [];
        foreach (SentidoDoMotivo sentido in SentidosJaEscolhidos(ordem))
        {
            anteriores.Add(await EscolherAntesDaDisputaAsync(processoId, faseId, sentido));
        }

        MotivoDeDecisao disputado = await _chamadas.SemearMotivoDeIsencaoAsync(SentidoDoDisputado(ordem));

        return new Cenario(processoId, documentoId, faseId, anteriores, disputado);
    }

    private static SentidoDoMotivo[] SentidosJaEscolhidos(OrdemDeChegada ordem) =>
        ordem == OrdemDeChegada.InclusaoPrimeiro ? [SentidoDoMotivo.Desfavoravel] : [SentidoDoMotivo.Desfavoravel, SentidoDoMotivo.Favoravel];

    private static SentidoDoMotivo SentidoDoDisputado(OrdemDeChegada ordem) =>
        ordem == OrdemDeChegada.InclusaoPrimeiro ? SentidoDoMotivo.Favoravel : SentidoDoMotivo.Desfavoravel;

    private async Task<Guid> EscolherAntesDaDisputaAsync(Guid processoId, Guid faseId, SentidoDoMotivo sentido)
    {
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoDeIsencaoAsync(sentido);
        using HttpResponseMessage inclusao = await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaFase(processoId, faseId, motivo.Id));
        inclusao.StatusCode.Should().Be(HttpStatusCode.NoContent, await inclusao.Content.ReadAsStringAsync());

        return motivo.Id;
    }

    private async Task<Desfecho> DisputarAsync(Cenario cenario, OrdemDeChegada ordem)
    {
        await using SessaoQueRetemOProcesso retida = await SessaoQueRetemOProcesso.AbrirAsync(_fixture.ConnectionString, cenario.ProcessoId);
        await using OperacoesNaFilaDoBloqueio fila = new(HostComEsperaLongaPeloBloqueio(), retida, EsperaDoTeste, _fixture.MarcarComoInutilizavel);

        await fila.EnfileirarAsync(ComandosNaOrdemDeChegada(cenario, ordem));
        IReadOnlyList<Result> resultados = await fila.LiberarEAguardarAsync();

        return ordem == OrdemDeChegada.InclusaoPrimeiro
            ? new Desfecho(Inclusao: resultados[0], Publicacao: resultados[1])
            : new Desfecho(Inclusao: resultados[1], Publicacao: resultados[0]);
    }

    private WebApplicationFactory<HostAssemblyMarker> HostComEsperaLongaPeloBloqueio() =>
        _fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuracao) =>
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TravaDoProcesso:EsperaMaxima"] = EsperaDoBloqueio.ToString("c", CultureInfo.InvariantCulture),
            })));

    private static object[] ComandosNaOrdemDeChegada(Cenario cenario, OrdemDeChegada ordem)
    {
        IncluirMotivoNoProcessoCommand inclusao = new(cenario.ProcessoId, LugarDaDecisao.DaFase(cenario.FaseId), cenario.Disputado.Id);
        PublicarProcessoSeletivoCommand publicacao = new(
            cenario.ProcessoId, Numero: null, PeriodoInscricaoInicio: null, PeriodoInscricaoFim: null, cenario.DocumentoId, DadosDoAtoDeTeste.Padrao);

        return ordem == OrdemDeChegada.InclusaoPrimeiro ? [inclusao, publicacao] : [publicacao, inclusao];
    }

    private static void ConferirResultadosDasOperacoes(Desfecho desfecho, OrdemDeChegada ordem)
    {
        if (ordem == OrdemDeChegada.InclusaoPrimeiro)
        {
            desfecho.Inclusao.IsSuccess.Should().BeTrue(desfecho.Inclusao.Error?.Message);
            desfecho.Publicacao.IsSuccess.Should().BeTrue(
                $"a publicação conferiu o motivo favorável incluído antes dela, senão recusaria por cobertura insuficiente: {desfecho.Publicacao.Error?.Code}");
            return;
        }

        desfecho.Publicacao.IsSuccess.Should().BeTrue(desfecho.Publicacao.Error?.Message, "o processo já estava coberto sem o motivo disputado");
        desfecho.Inclusao.IsFailure.Should().BeTrue("a inclusão que chega depois da publicação não entra");
        desfecho.Inclusao.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    private async Task ConferirEstadoFinalAsync(Cenario cenario, OrdemDeChegada ordem)
    {
        EstadoFinal estado = await LerEstadoFinalAsync(cenario.ProcessoId);

        estado.Status.Should().Be(StatusProcesso.Publicado);
        estado.Versoes.Should().ContainSingle("a publicação criou uma só versão, a primeira").Which.NumeroVersao.Should().Be(1);
        ConferirEscolhaSemDuplicidadeNemOrfa(estado.Escolhas);
        estado.Escolhas.Select(e => e.MotivoDeDecisaoOrigemId).Should().BeEquivalentTo(
            MotivosEsperados(cenario, ordem), "a escolha final é a que a ordem da fila deixou: com a inclusão antes, os anteriores e o disputado; com a publicação antes, só os anteriores");
        await ConferirQueAVersaoPublicadaTrazAEscolhaAsync(cenario.ProcessoId, estado.Escolhas);
    }

    private static Guid[] MotivosEsperados(Cenario cenario, OrdemDeChegada ordem) =>
        ordem == OrdemDeChegada.InclusaoPrimeiro ? [.. cenario.Anteriores, cenario.Disputado.Id] : [.. cenario.Anteriores];

    private static void ConferirEscolhaSemDuplicidadeNemOrfa(IReadOnlyList<MotivoDoProcesso> escolhas)
    {
        escolhas.GroupBy(e => (e.MotivoDeDecisaoOrigemId, e.Lugar)).Should().OnlyContain(g => g.Count() == 1, "o motivo é escolhido uma só vez em cada lugar");
        escolhas.Should().OnlyContain(
            e => e.Vigencia.VersaoDoEditalDeEntrada == 1 && e.Vigencia.VersaoDoEditalDeSaida == null,
            "toda escolha gravada vale desde a primeira versão: não há escolha que fique de fora da versão publicada");
    }

    private async Task ConferirQueAVersaoPublicadaTrazAEscolhaAsync(Guid processoId, IReadOnlyList<MotivoDoProcesso> escolhas)
    {
        IReadOnlyList<JsonElement> naVersao1 = await _chamadas.LerAsync(processoId, versao: 1);

        naVersao1.Select(e => e.GetProperty("codigo").GetString()).Should().BeEquivalentTo(
            escolhas.Select(e => e.Codigo.Valor), "a leitura da versão publicada traz a mesma escolha gravada, nem a mais nem a menos");
    }

    private async Task<EstadoFinal> LerEstadoFinalAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return new EstadoFinal(
            await db.ProcessosSeletivos.AsNoTracking().Where(p => p.Id == processoId).Select(p => p.Status).SingleAsync(),
            await EscolhasDoTesteAsync(db, processoId),
            await db.VersoesConfiguracao.AsNoTracking().Where(v => v.ProcessoSeletivoId == processoId).ToListAsync());
    }

    // O processo semeado já escolhe o motivo da condição da regra de homologação; a escolha em disputa é só a dos motivos do teste.
    private static async Task<IReadOnlyList<MotivoDoProcesso>> EscolhasDoTesteAsync(UniPlusDbContext db, Guid processoId)
    {
        List<MotivoDoProcesso> escolhas = await db.MotivosDoProcesso.AsNoTracking().Where(m => m.ProcessoSeletivoId == processoId).ToListAsync();

        return [.. escolhas.Where(static m => m.Codigo.Valor.StartsWith("TEST_ICP_", StringComparison.Ordinal))];
    }

    private sealed record Cenario(Guid ProcessoId, Guid DocumentoId, Guid FaseId, IReadOnlyList<Guid> Anteriores, MotivoDeDecisao Disputado);

    private sealed record Desfecho(Result Inclusao, Result Publicacao);

    private sealed record EstadoFinal(StatusProcesso Status, IReadOnlyList<MotivoDoProcesso> Escolhas, IReadOnlyList<VersaoConfiguracao> Versoes);
}
