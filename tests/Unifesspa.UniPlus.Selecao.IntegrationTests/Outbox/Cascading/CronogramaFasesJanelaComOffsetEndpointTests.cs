namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A janela do cronograma de fases informada com offset diferente de UTC (issue #1124),
/// pelo HTTP e contra Postgres real — o contrato declara <c>inicio</c>/<c>fim</c> como
/// <c>date-time</c>, e um instante RFC 3339 é inequívoco qualquer que seja o offset com
/// que o cliente o escreva.
/// </summary>
/// <remarks>
/// <para>
/// Antes da correção, a coluna <c>timestamp with time zone</c> recebia o
/// <c>DateTimeOffset</c> com o offset original, o Npgsql recusava tudo que não fosse
/// <c>+00:00</c> no <c>SaveChanges</c>, e a exceção escapava como <c>500</c> — um payload
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
public sealed class CronogramaFasesJanelaComOffsetEndpointTests
{
    /// <summary>
    /// Fase de origem de data PROPRIA — a que exige janela (CA-07 da Story #851) e, por
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
    // Fase sem resultado publicado de proposito: o assunto aqui e o offset da janela,
    // e uma fase que produz resultado exigiria tambem o tipo de ato, ruido que nao
    // pertence a este teste. Desde que o catalogo de fases passou a nascer semeado,
    // HOMOLOGACAO chega com produz_resultado verdadeiro e o POST responderia 422.
    private const string CodigoFaseComJanela = "ENSALAMENTO";

    private static readonly DateTimeOffset InicioEsperadoUtc = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FimEsperadoUtc = new(2027, 1, 27, 21, 0, 0, TimeSpan.Zero);

    private readonly CascadingFixture _fixture;

    public CronogramaFasesJanelaComOffsetEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = "Janela com offset negativo, positivo ou UTC conclui em 204 e persiste o mesmo instante")]
    [InlineData("2027-01-25T08:00:00-03:00", "2027-01-27T18:00:00-03:00")]
    [InlineData("2027-01-25T16:30:00+05:30", "2027-01-28T02:30:00+05:30")]
    [InlineData("2027-01-25T11:00:00Z", "2027-01-27T21:00:00Z")]
    public async Task JanelaComOffset_204EPersisteInstanteEmUtc(string inicio, string fim)
    {
        Contexto ctx = await SemearAsync($"offset {inicio}");

        HttpResponseMessage resposta = await ctx.PutCronogramaFasesAsync(inicio, fim);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"'{inicio}' é um instante válido pelo contrato date-time — o offset é forma de escrita, não erro do cliente");

        FaseCronograma persistida = await ctx.ObterFasePersistidaAsync();
        persistida.Inicio.Should().Be(InicioEsperadoUtc, "o instante informado pelo cliente é preservado");
        persistida.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero, "a janela é normalizada para UTC antes de persistir");
        persistida.Fim.Should().Be(FimEsperadoUtc);
        persistida.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);

        FaseCronogramaDto lida = await ctx.ObterFaseLidaPelaApiAsync();
        lida.Inicio.Should().Be(InicioEsperadoUtc, "a leitura devolve o mesmo instante que a gravação recebeu");
        lida.Fim.Should().Be(FimEsperadoUtc);
    }

    [Fact(DisplayName = "Redefinir a MESMA fase com outro offset atualiza o instante — o caminho de reconciliação também normaliza")]
    public async Task RedefinirMesmaFaseComOutroOffset_AtualizaInstanteEmUtc()
    {
        Contexto ctx = await SemearAsync(nameof(RedefinirMesmaFaseComOutroOffset_AtualizaInstanteEmUtc));

        HttpResponseMessage primeira = await ctx.PutCronogramaFasesAsync(
            "2027-01-25T11:00:00Z", "2027-01-27T21:00:00Z");
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Mesma fase canônica: DefinirCronogramaFases reconcilia a linha existente por
        // FaseCanonicaOrigemId (AtualizarSnapshot) em vez de recriá-la.
        HttpResponseMessage segunda = await ctx.PutCronogramaFasesAsync(
            "2027-02-01T05:00:00-03:00", "2027-02-03T15:00:00-03:00");
        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);

        FaseCronograma persistida = await ctx.ObterFasePersistidaAsync();
        persistida.Inicio.Should().Be(new DateTimeOffset(2027, 2, 1, 8, 0, 0, TimeSpan.Zero));
        persistida.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero);
        persistida.Fim.Should().Be(new DateTimeOffset(2027, 2, 3, 18, 0, 0, TimeSpan.Zero));
        persistida.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid FaseCanonicaId)
    {
        public async Task<HttpResponseMessage> PutCronogramaFasesAsync(string inicio, string fim)
        {
            // Corpo cru para fixar o TEXTO RFC 3339 que trafega — serializar um
            // DateTimeOffset deixaria o offset a cargo do serializador do teste, e é
            // justamente o offset do payload que está sob prova.
            string faseCanonicaId = FaseCanonicaId.ToString();
            string corpo = $$"""
                [{
                  "ordem": 1,
                  "faseCanonicaId": "{{faseCanonicaId}}",
                  "inicio": "{{inicio}}",
                  "fim": "{{fim}}",
                  "atoProduzidoCodigo": null,
                  "tiposBancaIds": [],
                  "regraRecurso": null
                }]
                """;

            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/cronograma-fases", UriKind.Relative))
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<FaseCronograma> ObterFasePersistidaAsync()
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            return await db.Set<FaseCronograma>().AsNoTracking()
                .SingleAsync(f => f.ProcessoSeletivoId == ProcessoId)
                .ConfigureAwait(false);
        }

        public async Task<FaseCronogramaDto> ObterFaseLidaPelaApiAsync()
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}", UriKind.Relative));
            Autenticar(request);
            using HttpResponseMessage resposta = await Client.SendAsync(request).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            ProcessoSeletivoDto? processo = await resposta.Content
                .ReadFromJsonAsync<ProcessoSeletivoDto>(JsonDoContrato.Opcoes).ConfigureAwait(false);
            return processo!.CronogramaFases.Single();
        }
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

        Guid processoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            ProcessoSeletivo processo = ProcessoSeletivo.Criar(
                $"PS janela com offset — {nome} {Guid.CreateVersion7()}",
                Unifesspa.UniPlus.Selecao.Domain.Enums.TipoProcesso.SiSU,
                OrigemCandidatos.InscricaoPropria,
                Guid.CreateVersion7(),
                UnidadeAdministradoraSnapshot.Criar(
                    "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
            await db.ProcessosSeletivos.AddAsync(processo).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);
            processoId = processo.Id;
        }

        return new Contexto(api, client, processoId, faseCanonicaId);
    }

    /// <summary>
    /// O código da fase canônica é único entre as vivas e o Postgres da collection é
    /// compartilhado — cada caso desta suíte reaproveita a fase que o primeiro semeou, em
    /// vez de inserir de novo.
    /// </summary>
    private static async Task<Guid> ObterOuCriarFaseCanonicaAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext config = scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        // O catálogo de fases vivas é pequeno — materializar é mais barato que
        // traduzir a comparação do value object Codigo para SQL.
        List<FaseCanonica> vivas = await config.FasesCanonicas.AsNoTracking()
            .ToListAsync().ConfigureAwait(false);
        FaseCanonica? existente = vivas.Find(f =>
            string.Equals(f.Codigo.Valor, CodigoFaseComJanela, StringComparison.Ordinal));
        if (existente is not null)
        {
            return existente.Id;
        }

        Result<FaseCanonica> criada = FaseCanonica.Criar(
            CodigoFaseComJanela, "Ensalamento", null, "CEPS",
            agrupaEtapas: false, permiteComplementacao: false, baseLegal: null,
            produzResultado: false, resultadoDefinitivo: false, coletaInscricao: false,
            origemData: "PROPRIA");
        criada.IsSuccess.Should().BeTrue(criada.Error?.Message);

        config.FasesCanonicas.Add(criada.Value!);
        await config.SaveChangesAsync().ConfigureAwait(false);
        return criada.Value!.Id;
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
