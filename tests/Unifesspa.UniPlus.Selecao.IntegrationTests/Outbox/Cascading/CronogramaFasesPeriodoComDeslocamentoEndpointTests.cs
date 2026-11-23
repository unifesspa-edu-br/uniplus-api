namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

/// <summary>
/// O período do cronograma de fases informado com deslocamento diferente de UTC (issue #1124),
/// pelo HTTP e contra Postgres real — o contrato declara <c>inicio</c>/<c>fim</c> como
/// <c>date-time</c>, e um instante RFC 3339 é inequívoco qualquer que seja o offset com
/// que o cliente o escreva.
/// </summary>
/// <remarks>
/// <para>
/// Antes da correção, a coluna <c>timestamp with time zone</c> recebia o
/// <c>DateTimeOffset</c> com o offset original, o Npgsql recusava tudo que não fosse
/// <c>+00:00</c> no <c>SaveChanges</c>, e a exceção escapava como <c>500</c> — um corpo da requisição
/// válido pelo contrato virava erro de servidor.
/// </para>
/// <para>
/// O teste exige Postgres real: o defeito era da escrita da coluna, invisível para
/// qualquer teste que pare no domínio ou em provider em memória.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CronogramaFasesPeriodoComDeslocamentoEndpointTests
{
    /// <summary>
    /// Fase de origem de data NO_EDITAL — a que exige período (CA-07 da Story #851) e, por
    /// isso, a que exercita a gravação de <c>inicio</c>/<c>fim</c>. Não produz resultado,
    /// então não arrasta a exigência de ato produzido para o arranjo.
    /// </summary>
    /// <remarks>
    /// Deliberadamente NÃO é <c>INSCRICAO</c> nem <c>AVALIACAO</c>: o Postgres da collection
    /// é compartilhado, o código da fase canônica é único entre as vivas, e
    /// <see cref="EnvelopeFechadoE2ETests"/> insere esses dois sem conferir se já existem —
    /// semear qualquer um deles aqui faria aquela suíte violar a unicidade dependendo da
    /// ordem de execução.
    /// </remarks>
    // Fase sem produto declarado de propósito: o assunto aqui é o deslocamento do período, e uma fase
    // que produz resultado exigiria também o tipo de ato, ruído que não pertence a este teste.
    private const string CodigoFaseComPeriodo = "ENSALAMENTO";

    private static readonly DateTimeOffset InicioEsperadoUtc = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FimEsperadoUtc = new(2027, 1, 27, 21, 0, 0, TimeSpan.Zero);

    private readonly CascadingFixture _fixture;

    public CronogramaFasesPeriodoComDeslocamentoEndpointTests(CascadingFixture ambienteDeTeste) => _fixture = ambienteDeTeste;

    [Theory(DisplayName = "Período com deslocamento negativo, positivo ou UTC conclui em 204 e persiste o mesmo instante")]
    [InlineData("2027-01-25T08:00:00-03:00", "2027-01-27T18:00:00-03:00")]
    [InlineData("2027-01-25T16:30:00+05:30", "2027-01-28T02:30:00+05:30")]
    [InlineData("2027-01-25T11:00:00Z", "2027-01-27T21:00:00Z")]
    public async Task PeriodoComDeslocamento_204EPersisteInstanteEmUtc(string inicio, string fim)
    {
        Contexto ctx = await SemearAsync($"offset {inicio}");
        HttpResponseMessage resposta = await ctx.PutCronogramaFasesAsync(inicio, fim);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"'{inicio}' é um instante válido pelo contrato date-time — o offset é forma de escrita, não erro do cliente");

        AfirmarPeriodoPersistidoEmUtc(await ctx.ObterFasePersistidaAsync(), InicioEsperadoUtc, FimEsperadoUtc);

        FaseCronogramaDto lida = await ctx.ObterFaseLidaPelaApiAsync();
        lida.Cronograma.Inicio.Should().Be(InicioEsperadoUtc, "a leitura devolve o mesmo instante que a gravação recebeu");
        lida.Cronograma.Fim.Should().Be(FimEsperadoUtc);
    }

    [Fact(DisplayName = "Redefinir a MESMA fase com outro offset atualiza o instante — o caminho de reconciliação também normaliza")]
    public async Task RedefinirMesmaFaseComOutroOffset_AtualizaInstanteEmUtc()
    {
        Contexto ctx = await SemearAsync(nameof(RedefinirMesmaFaseComOutroOffset_AtualizaInstanteEmUtc));
        HttpResponseMessage primeira = await ctx.PutCronogramaFasesAsync("2027-01-25T11:00:00Z", "2027-01-27T21:00:00Z");
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Mesma fase canônica: DefinirCronogramaFases reconcilia a linha existente por
        // FaseCanonicaOrigemId (AtualizarDe) em vez de recriá-la.
        HttpResponseMessage segunda = await ctx.PutCronogramaFasesAsync("2027-02-01T05:00:00-03:00", "2027-02-03T15:00:00-03:00");
        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);

        AfirmarPeriodoPersistidoEmUtc(
            await ctx.ObterFasePersistidaAsync(),
            new DateTimeOffset(2027, 2, 1, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 2, 3, 18, 0, 0, TimeSpan.Zero));
    }

    private static void AfirmarPeriodoPersistidoEmUtc(
        FaseCronograma persistida, DateTimeOffset inicioEsperado, DateTimeOffset fimEsperado)
    {
        persistida.Inicio.Should().Be(inicioEsperado, "o instante informado pelo cliente é preservado");
        persistida.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero, "o período é normalizado para UTC antes de persistir");
        persistida.Fim.Should().Be(fimEsperado);
        persistida.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid FaseCanonicaId)
    {
        public Task<HttpResponseMessage> PutCronogramaFasesAsync(string inicio, string fim) =>
            EnviarCronogramaComCorpoCruAsync(Client, ProcessoId, CorpoDaFase(inicio, fim));

        public async Task<FaseCronograma> ObterFasePersistidaAsync()
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            return await db.Set<FaseCronograma>().AsNoTracking()
                .SingleAsync(f => f.ProcessoSeletivoId == ProcessoId)
                .ConfigureAwait(false);
        }

        public async Task<FaseCronogramaDto> ObterFaseLidaPelaApiAsync() =>
            (await LerProcessoPelaApiAsync(Client, ProcessoId).ConfigureAwait(false)).CronogramaFases.Single();

        private string CorpoDaFase(string inicio, string fim) => $$"""
            [{
              "ordem": 1,
              "faseCanonicaId": "{{FaseCanonicaId}}",
              "inicio": "{{inicio}}",
              "fim": "{{fim}}",
              "produtos": [],
              "emiteParecerIndividual": false,
              "bancasRequeridas": [],
              "regraRecurso": null
            }]
            """;
    }

    /// <summary>
    /// Processo em rascunho <b>sem cronograma</b> — o PUT sob teste é o primeiro a escrevê-lo.
    /// Um processo que já trouxesse uma fase em outra ordem faria o PUT remover uma linha e
    /// inserir outra no mesmo slot único (<c>ux_fases_cronograma_processo_ordem</c>), um
    /// segundo assunto que não é o desta issue.
    /// </summary>
    private async Task<Contexto> SemearAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();

        Guid faseCanonicaId = await ObterOuCriarFaseCanonicaAsync(api);
        Guid processoId = await SemearProcessoEmRascunhoAsync(
            api, $"PS período com deslocamento — {nome}", OrigemCandidatos.InscricaoPropria);

        return new Contexto(api, client, processoId, faseCanonicaId);
    }

    /// <summary>
    /// O código da fase canônica é único entre as vivas e o Postgres da collection é
    /// compartilhado — cada caso desta suíte reaproveita a fase que o primeiro semeou, em
    /// vez de inserir de novo.
    /// </summary>
    private static async Task<Guid> ObterOuCriarFaseCanonicaAsync(CascadingApiFactory api)
    {
        FaseCanonica? existente = await ProcurarFaseCanonicaAsync(api, CodigoFaseComPeriodo);
        if (existente is not null)
        {
            return existente.Id;
        }

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        return await CriarFaseDeEnsalamentoAsync(scope.ServiceProvider.GetRequiredService<UniPlusDbContext>());
    }

    private static async Task<Guid> CriarFaseDeEnsalamentoAsync(UniPlusDbContext config)
    {
        Result<FaseCanonica> criada = FaseCanonica.Criar(
            CodigoFaseComPeriodo, "Ensalamento", null, baseLegal: null, origemData: "NO_EDITAL",
            sinalizadores: new Unifesspa.UniPlus.Configuracao.Domain.ValueObjects.SinalizadoresDaFase(
                AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false));
        criada.IsSuccess.Should().BeTrue(criada.Error?.Message);

        config.FasesCanonicas.Add(criada.Value!);
        await config.SaveChangesAsync().ConfigureAwait(false);
        return criada.Value!.Id;
    }
}
