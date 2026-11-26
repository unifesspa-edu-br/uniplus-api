namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// <b>A revisão do filtro muda exatamente quando muda o que o percurso percorre.</b>
/// </summary>
/// <remarks>
/// <para>
/// Quem compõe a vitrine (a Portal) pagina sobre a de Seleção e precisa saber, entre uma página e
/// outra, se a coleção avançou — a ADR-0131 manda avisar e recomeçar, em vez de entregar uma lista
/// com item repetido ou sumido. Os dois erros possíveis são simétricos: um marcador que não muda
/// quando a coleção muda deixa a inconsistência passar; um que muda sem mudança derruba percursos
/// íntegras.
/// </para>
/// <para>
/// Correm contra Postgres real porque a revisão é lida do filtro que o banco aplica, e é a
/// tradução desse filtro que decide quais linhas entram no marcador.
/// </para>
/// </remarks>
public sealed class RevisaoDosCertamesPublicadosPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);


    private const string VersaoServida = "1";

    private readonly ProcessoSeletivoDbFixture _fixture;

    private readonly List<CertameDivulgado> _semeados = [];

    public RevisaoDosCertamesPublicadosPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE selecao.certames_divulgados");

        // Três abertos com prazos distintos e dois encerrados: dá página de continuação e dois
        // filtros por situação que não se tocam.
        _semeados.Add(Divulgado(Agora.AddDays(-5), Agora.AddDays(20)));
        _semeados.Add(Divulgado(Agora.AddDays(-5), Agora.AddDays(40)));
        _semeados.Add(Divulgado(Agora.AddDays(-5), Agora.AddDays(60)));
        _semeados.Add(Divulgado(Agora.AddDays(-60), Agora.AddDays(-10)));
        _semeados.Add(Divulgado(Agora.AddDays(-60), Agora.AddDays(-20)));

        context.CertamesDivulgados.AddRange(_semeados);
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Sem mudança na coleção, a revisão se repete entre leituras e entre páginas da mesmo percurso")]
    public async Task Revisao_SemMudanca_DeveSerEstavel()
    {
        PaginaDaVitrine primeira = await ListarAsync(new FiltroDosCertamesPublicados(), Agora, limite: 2);
        PaginaDaVitrine repetida = await ListarAsync(new FiltroDosCertamesPublicados(), Agora, limite: 2);
        PaginaDaVitrine segunda = await ContinuarAsync(new FiltroDosCertamesPublicados(), primeira, limite: 2);

        repetida.Revisao.Should().Be(primeira.Revisao, "nada mudou entre as duas leituras");
        segunda.Revisao.Should().Be(primeira.Revisao,
            "as páginas da mesmo percurso descrevem o mesmo filtro — marcador diferente faria quem compõe recomeçar sem motivo");
        segunda.Itens.Should().NotBeEmpty("a continuação precisa existir para o teste valer");
    }

    [Fact(DisplayName = "Divulgar um certame que entra no filtro muda a revisão")]
    public async Task Revisao_QuandoCertameEntraNoFiltro_DeveMudar()
    {
        string antes = (await ListarAsync(new FiltroDosCertamesPublicados(), Agora)).Revisao;

        await AdicionarAsync(Divulgado(Agora.AddDays(-1), Agora.AddDays(30)));

        (await ListarAsync(new FiltroDosCertamesPublicados(), Agora)).Revisao.Should().NotBe(antes);
    }

    [Fact(DisplayName = "Filtros diferentes têm revisões independentes: mudar um não mexe no outro")]
    public async Task Revisao_QuandoMudaOutroFiltro_NaoDeveMudar()
    {
        FiltroDosCertamesPublicados abertas = new(SituacaoDoCertame.InscricoesAbertas);
        FiltroDosCertamesPublicados encerradas = new(SituacaoDoCertame.Encerradas);

        string abertasAntes = (await ListarAsync(abertas, Agora)).Revisao;
        string encerradasAntes = (await ListarAsync(encerradas, Agora)).Revisao;

        await AdicionarAsync(Divulgado(Agora.AddDays(-90), Agora.AddDays(-30)));

        (await ListarAsync(abertas, Agora)).Revisao.Should().Be(abertasAntes,
            "o certame novo é encerrado e não entra no filtro das abertas");
        (await ListarAsync(encerradas, Agora)).Revisao.Should().NotBe(encerradasAntes);
    }

    [Fact(DisplayName = "Cruzar a fronteira do período de inscrição muda a revisão, sem nenhuma linha mudar")]
    public async Task Revisao_QuandoORelogioCruzaOPeriodo_DeveMudar()
    {
        // O primeiro aberto encerra em 20 dias: um dia depois ele muda de grupo e de posição.
        string antes = (await ListarAsync(new FiltroDosCertamesPublicados(), Agora)).Revisao;
        string depois = (await ListarAsync(new FiltroDosCertamesPublicados(), Agora.AddDays(21))).Revisao;

        depois.Should().NotBe(antes);
    }

    [Fact(DisplayName = "Retificação que reordena muda a revisão, mesmo divulgada com instante anterior ao mais recente")]
    public async Task Revisao_QuandoRetificacaoReordena_DeveMudar()
    {
        // O instante de divulgação mais antigo que o máximo corrente e a contagem inalterada são
        // exatamente o caso que a contagem mais o instante máximo não alcançavam.
        string antes = (await ListarAsync(new FiltroDosCertamesPublicados(), Agora)).Revisao;

        await RetificarUltimoCertameAsync();

        (await ListarAsync(new FiltroDosCertamesPublicados(), Agora)).Revisao.Should().NotBe(antes);
    }

    private async Task RetificarUltimoCertameAsync()
    {
        await using (UniPlusDbContext context = _fixture.CreateDbContext())
        {
            CertameDivulgado ultimo = await context.CertamesDivulgados.SingleAsync(c => c.Id == _semeados[2].Id);
            ultimo.Avancar(
                numeroVersao: 2,
                Guid.CreateVersion7(),
                new string('b', 64),
                VersaoServida,
                new FacetasDoCertameDivulgado(
                    ultimo.IdentificadorLegivel, "Certame retificado", "001/2026", ["AC"], Agora.AddDays(-5), Agora.AddDays(10)),
                """{"nome":"Certame retificado"}""",
                Agora.AddDays(-30));
            await context.SaveChangesAsync();
        }
    }

    private async Task<PaginaDaVitrine> ListarAsync(FiltroDosCertamesPublicados filtro, DateTimeOffset instante, int limite = 50)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        return await repository.ListarVitrineAsync(Consulta(instante, filtro, [], false), null, null, limite, PaginationDirection.Next, CancellationToken.None);
    }

    private async Task<PaginaDaVitrine> ContinuarAsync(FiltroDosCertamesPublicados filtro, PaginaDaVitrine anterior, int limite)
    {
        (string sortKey, Guid id) = anterior.Proximo!.Value;

        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        // O instante da primeira página viaja no ponto de continuação; o que se passa aqui é ignorado na
        // continuação, e passar outro prova que a revisão não depende dele.
        return await repository.ListarVitrineAsync(Consulta(Agora.AddDays(365), filtro, [], false), sortKey, id, limite, PaginationDirection.Next, CancellationToken.None);
    }

    private async Task AdicionarAsync(CertameDivulgado divulgado)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        context.CertamesDivulgados.Add(divulgado);
        await context.SaveChangesAsync();
    }

    private static CertameDivulgado Divulgado(DateTimeOffset inscricoesDe, DateTimeOffset inscricoesAte) =>
        CertameDivulgado.Criar(
            Guid.CreateVersion7(),
            numeroVersao: 1,
            Guid.CreateVersion7(),
            new string('a', 64),
            VersaoServida,
            new FacetasDoCertameDivulgado(
                IdentificadoresDeTeste.Novo().Valor, "Certame de revisão", "001/2026", ["AC"], inscricoesDe, inscricoesAte),
            """{"nome":"Certame de revisão"}""",
            Agora);
    private static ConsultaDosCertamesPublicados Consulta(
        DateTimeOffset instante, FiltroDosCertamesPublicados filtro, IReadOnlyList<SortField> ordenacao, bool contadores) =>
        new(instante, filtro, ordenacao, contadores, VersaoServida);

}
