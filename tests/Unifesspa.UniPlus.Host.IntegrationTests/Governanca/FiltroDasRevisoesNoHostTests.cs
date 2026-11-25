namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

/// <summary>
/// O filtro da situação na listagem das revisões, pelo host real: filtra no servidor antes da paginação, e cada página
/// pedida traz só revisões da situação, até a última. Três revisões concluídas (sem item pendente) e uma em andamento (com
/// a concessão de Leandro Viana a decidir); Marlene Martins conduz.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class FiltroDasRevisoesNoHostTests : PedidosDeConcessaoTestBase
{
    private const string Revisoes = "/api/autorizacao/revisoes";

    public FiltroDasRevisoesNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O filtro de concluídas percorre as páginas menores que o total sem trazer a revisão em andamento")]
    public async Task Concluidas_PaginaMenorQueOTotal_SoConcluidas()
    {
        (Guid[] concluidas, Guid emAndamento) = await SemearRevisoesAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        (Guid Id, string Situacao)[] lidas = [.. (await PaginasAsync(marlene, $"{Revisoes}?situacao=CONCLUIDA&limit=2")).SelectMany(static p => p)];

        lidas.Select(static r => r.Id).Should().BeEquivalentTo(concluidas).And.NotContain(emAndamento);
        lidas.Should().OnlyContain(static r => r.Situacao == "CONCLUIDA");
    }

    [Fact(DisplayName = "O filtro de em andamento traz só a revisão com item pendente")]
    public async Task EmAndamento_SoAComItemPendente()
    {
        (_, Guid emAndamento) = await SemearRevisoesAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        (Guid Id, string Situacao)[] lidas = [.. (await PaginasAsync(marlene, $"{Revisoes}?situacao=EM_ANDAMENTO&limit=2")).SelectMany(static p => p)];

        lidas.Should().Equal((emAndamento, "EM_ANDAMENTO"));
    }

    [Theory]
    [InlineData("?limit=2")]
    [InlineData("?limit=2&situacao=")]
    public async Task Listar_SemSituacao_NaoFiltra(string filtro)
    {
        (Guid[] concluidas, Guid emAndamento) = await SemearRevisoesAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        (Guid Id, string Situacao)[] lidas = [.. (await PaginasAsync(marlene, $"{Revisoes}{filtro}")).SelectMany(static p => p)];

        lidas.Select(static r => r.Id).Should().BeEquivalentTo(concluidas.Append(emAndamento));
    }

    [Fact(DisplayName = "Situação composta só por espaços no filtro de revisões é recusada com 400")]
    public async Task Listar_SituacaoComEspacos_RecusaCom400()
    {
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpResponseMessage resposta = await marlene.GetAsync(new Uri($"{Revisoes}?limit=2&situacao=%20%20", UriKind.Relative));

        await ProblemaRegistrado.ConferirAsync(resposta, HttpStatusCode.BadRequest,
            "uniplus.autorizacao.revisao.filtro_de_situacao_invalido",
            "Situação do filtro de revisões inválida",
            "Situação inválida. Use um dos códigos publicados para a situação da revisão.");
    }

    [Fact(DisplayName = "Com a revisão em andamento entre as concluídas, as páginas do filtro saem cheias: o filtro vem antes da paginação")]
    public async Task Intercaladas_PaginasCheiasDaSituacao()
    {
        Guid[] concluidas = await SemearIntercaladasAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        (Guid Id, string Situacao)[][] paginas = await PaginasAsync(marlene, $"{Revisoes}?situacao=CONCLUIDA&limit=2");

        paginas.Select(static p => p.Length).Should().Equal(2, 1);
        paginas.SelectMany(static p => p).Select(static r => r.Id).Should().BeEquivalentTo(concluidas);
        (await PaginasAsync(marlene, $"{Revisoes}?situacao=EM_ANDAMENTO&limit=1")).Should().ContainSingle();
    }

    [Theory]
    [InlineData("concluida")]
    [InlineData("%20CONCLUIDA%20")]
    [InlineData("pendente")]
    [InlineData("todas")]
    public async Task SituacaoDesconhecida_RecebeRecusaSemValor(string situacao)
    {
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        using HttpResponseMessage resposta = await marlene.GetAsync(new Uri($"{Revisoes}?situacao={situacao}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.autorizacao.revisao.filtro_de_situacao_invalido");
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("detail").GetString().Should()
            .Be("Situação inválida. Use um dos códigos publicados para a situação da revisão.")
            .And.NotContain(situacao).And.NotContain("EM_ANDAMENTO").And.NotContain("CONCLUIDA");
    }

    private async Task<(Guid[] Concluidas, Guid EmAndamento)> SemearRevisoesAsync()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        await SemearConcessaoAsync(Leandro, perfil, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));
        Guid concessao = (await ConcessoesAsync(Leandro, perfil)).Single().Id;
        Guid[] concluidas = [await SemearRevisaoAsync([]), await SemearRevisaoAsync([]), await SemearRevisaoAsync([])];

        return (concluidas, await SemearRevisaoAsync([concessao]));
    }

    // Uma concluída, a em andamento e mais duas concluídas, nessa ordem de criação: em qualquer sentido da ordenação, a
    // paginação feita antes do filtro deixaria uma página de concluídas pela metade antes da última.
    private async Task<Guid[]> SemearIntercaladasAsync()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        await SemearConcessaoAsync(Leandro, perfil, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));
        Guid concessao = (await ConcessoesAsync(Leandro, perfil)).Single().Id;
        Guid primeira = await SemearRevisaoAsync([]);
        await SemearRevisaoAsync([concessao]);

        return [primeira, await SemearRevisaoAsync([]), await SemearRevisaoAsync([])];
    }

    private async Task<Guid> SemearRevisaoAsync(Guid[] concessoes)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(IdentidadeDe(Marlene), concessoes, DateTimeOffset.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IRevisaoDeAcessosRepository>().AdicionarAsync(revisao);
        await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();

        return revisao.Id;
    }

    // As páginas da listagem, uma a uma pelo cursor do cabeçalho Link.
    private static async Task<(Guid Id, string Situacao)[][]> PaginasAsync(HttpClient client, string primeira)
    {
        List<(Guid, string)[]> paginas = [];
        for (Uri? pagina = new(primeira, UriKind.Relative); pagina is not null;)
        {
            using HttpResponseMessage resposta = await client.GetAsync(pagina);
            JsonElement itens = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
            itens.GetArrayLength().Should().BeLessThanOrEqualTo(2);
            paginas.Add([.. itens.EnumerateArray().Select(static i => (i.GetProperty("id").GetGuid(), i.GetProperty("situacao").GetString()!))]);
            pagina = resposta.Headers.TryGetValues("Link", out IEnumerable<string>? links) ? Proxima(links) : null;
        }

        return [.. paginas];
    }

    private static Uri? Proxima(IEnumerable<string> links) =>
        links.SelectMany(static l => l.Split(',')).Select(static l => l.Trim())
            .Where(static l => l.EndsWith("rel=\"next\"", StringComparison.Ordinal))
            .Select(static l => new Uri(l[(l.IndexOf('<', StringComparison.Ordinal) + 1)..l.IndexOf('>', StringComparison.Ordinal)], UriKind.RelativeOrAbsolute))
            .FirstOrDefault();
}
