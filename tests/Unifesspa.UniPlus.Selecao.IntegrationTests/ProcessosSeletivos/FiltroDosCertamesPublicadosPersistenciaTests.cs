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
/// <b>O filtro por situação no banco diz o mesmo que a regra que classifica o item.</b>
/// </summary>
/// <remarks>
/// <para>
/// A partição das quatro situações é enunciada uma vez, em
/// <see cref="SituacaoDasInscricoesDoCertame.Classificar"/>, e precisa ser reescrita como predicado traduzível
/// para SQL — o banco não pode chamar o método. São duas expressões da mesma regra, e duas
/// expressões divergem em silêncio.
/// </para>
/// <para>
/// O sintoma da divergência não é erro: é o item vir marcado com uma situação e ter sido listado
/// sob outra, ou um contador não bater com o tamanho da lista que o próprio número promete. Estes
/// testes correm contra Postgres real porque é a tradução para SQL que está sob suspeita — em
/// memória, os dois predicados seriam o mesmo código.
/// </para>
/// </remarks>
public sealed class FiltroDosCertamesPublicadosPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);


    /// <summary>Versão do documento público que a consulta sabe servir — a que os semeados gravam.</summary>
    private const string VersaoServida = "1";

    /// <summary>
    /// Bordas em dias relativos ao instante da consulta: antes, em cima e depois de cada ponto que
    /// a regra usa — a abertura, o encerramento e o limiar dos últimos dias.
    /// </summary>
    private static readonly int[] Deslocamentos = [-30, -8, -7, -1, 0, 1, 3, 7, 8, 30];

    private readonly ProcessoSeletivoDbFixture _fixture;

    private readonly List<CertameDivulgado> _semeados = [];

    public FiltroDosCertamesPublicadosPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE selecao.certames_divulgados");

        // Toda combinação das bordas, inclusive perÃ­odos invertidos.
        foreach (int de in Deslocamentos)
        {
            foreach (int ate in Deslocamentos)
            {
                _semeados.Add(Divulgado(Agora.AddDays(de), Agora.AddDays(ate)));
            }
        }

        context.CertamesDivulgados.AddRange(_semeados);
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "O filtro no banco devolve exatamente o que a regra classifica naquela situação")]
    [InlineData(SituacaoDoCertame.EmBreve)]
    [InlineData(SituacaoDoCertame.InscricoesAbertas)]
    [InlineData(SituacaoDoCertame.UltimosDias)]
    [InlineData(SituacaoDoCertame.Encerradas)]
    public async Task ListarVitrine_QuandoFiltraPorSituacao_DeveConcordarComAClassificacao(SituacaoDoCertame situacao)
    {
        IReadOnlyList<Guid> doBanco = await ListarAsync(situacao);

        Guid[] daRegra = [.. _semeados.Where(c => Classificar(c) == situacao).Select(static c => c.Id)];

        doBanco.Should().BeEquivalentTo(daRegra,
            "o predicado que o banco aplica e a regra que marca o item são a mesma partição — se discordam, "
            + "o item aparece sob um filtro e vem marcado com outro");
    }

    [Fact(DisplayName = "As quatro situações particionam a vitrine: nenhum certame fica de fora nem aparece duas vezes")]
    public async Task ListarVitrine_QuandoPercorreAsQuatroSituacoes_DeveParticionarOConjunto()
    {
        List<Guid> reunidos = [];
        foreach (SituacaoDoCertame situacao in Enum.GetValues<SituacaoDoCertame>())
        {
            reunidos.AddRange(await ListarAsync(situacao));
        }

        IReadOnlyList<Guid> semFiltro = await ListarAsync(situacao: null);

        reunidos.Should().OnlyHaveUniqueItems("um certame em duas situações apareceria em duas abas e contaria duas vezes");
        reunidos.Should().BeEquivalentTo(semFiltro, "um certame em situação nenhuma some da vitrine quando ela é filtrada");
    }

    [Fact(DisplayName = "Cada contador é o tamanho da lista que ele promete, e os quatro somam o total")]
    public async Task ContarPorSituacao_QuandoHaDivulgadosEmTodasAsSituacoes_DeveBaterComOTamanhoDaSituacao()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        ContadoresDaVitrine contadores = (await repository.ListarVitrineAsync(Consulta(Agora, new FiltroDosCertamesPublicados(), [], true), null, null, 1, PaginationDirection.Next, CancellationToken.None)).Contadores!.Value;

        // Contar por um critério e filtrar por outro faz o rótulo mentir sem nada quebrar.
        await ConferirTamanhoDasSituacoesAsync(contadores);

        (contadores.EmBreve + contadores.InscricoesAbertas + contadores.UltimosDias + contadores.Encerrados)
            .Should().Be(_semeados.Count, "as quatro situações particionam o conjunto divulgado");
    }

    private async Task ConferirTamanhoDasSituacoesAsync(ContadoresDaVitrine contadores)
    {
        contadores.EmBreve.Should().Be((await ListarAsync(SituacaoDoCertame.EmBreve)).Count);
        contadores.InscricoesAbertas.Should().Be((await ListarAsync(SituacaoDoCertame.InscricoesAbertas)).Count);
        contadores.UltimosDias.Should().Be((await ListarAsync(SituacaoDoCertame.UltimosDias)).Count);
        contadores.Encerrados.Should().Be((await ListarAsync(SituacaoDoCertame.Encerradas)).Count);
    }

    // Contar o que a página não pode mostrar faz o número ao lado do filtro prometer item que
    // filtro nenhum alcança. Num perÃ­odo de implantaÃ§Ã£o em fases, o documento gravado por um
    // processo já atualizado é ilegível para este — e a linha existe no banco.
    [Fact(DisplayName = "Certame de outra versão de projeção fica fora da página e da contagem")]
    public async Task ListarVitrine_QuandoHaLinhaDeOutraVersaoDeProjecao_DeveOmitiLaDosDois()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        await AdicionarCertameDeOutraVersaoAsync(context);

        PaginaDaVitrine pagina = await repository.ListarVitrineAsync(Consulta(Agora, new FiltroDosCertamesPublicados(), [], true), null, null, _semeados.Count + 10, PaginationDirection.Next, CancellationToken.None);

        ContadoresDaVitrine contadores = pagina.Contadores!.Value;

        pagina.Itens.Should().HaveCount(_semeados.Count, "a linha de outra versão não é servível por esta consulta");
        (contadores.EmBreve + contadores.InscricoesAbertas + contadores.UltimosDias + contadores.Encerrados)
            .Should().Be(_semeados.Count, "a contagem descreve a mesma coleção que a página");
    }

    private static async Task AdicionarCertameDeOutraVersaoAsync(UniPlusDbContext context)
    {
        context.CertamesDivulgados.Add(Divulgado(
            Agora.AddDays(-1), Agora.AddDays(30), versaoProjecao: "2"));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static SituacaoDoCertame Classificar(CertameDivulgado certame) =>
        SituacaoDasInscricoesDoCertame.Classificar(certame.InscricoesDe, certame.InscricoesAte, Agora, SituacaoDasInscricoesDoCertame.LimiarDosUltimosDias);

    private async Task<IReadOnlyList<Guid>> ListarAsync(SituacaoDoCertame? situacao)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(Consulta(Agora, new FiltroDosCertamesPublicados(situacao), [], false), null, null, Deslocamentos.Length * Deslocamentos.Length, PaginationDirection.Next, CancellationToken.None);

        return [.. pagina.Itens.Select(static c => c.Id)];
    }

    private static CertameDivulgado Divulgado(
        DateTimeOffset inscricoesDe, DateTimeOffset inscricoesAte, string versaoProjecao = VersaoServida) =>
        CertameDivulgado.Criar(
            Guid.CreateVersion7(),
            numeroVersao: 1,
            Guid.CreateVersion7(),
            new string('a', 64),
            versaoProjecao,
            new FacetasDoCertameDivulgado(IdentificadoresDeTeste.Novo().Valor, "Certame de filtro", "001/2026", ["AC"], inscricoesDe, inscricoesAte),
            """{"nome":"Certame de filtro"}""",
            Agora);
    private static ConsultaDosCertamesPublicados Consulta(
        DateTimeOffset instante, FiltroDosCertamesPublicados filtro, IReadOnlyList<SortField> ordenacao, bool contadores) =>
        new(instante, filtro, ordenacao, contadores, VersaoServida);

}
