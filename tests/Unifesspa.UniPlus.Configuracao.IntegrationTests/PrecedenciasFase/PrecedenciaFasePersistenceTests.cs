namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PrecedenciasFase;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta de <c>PrecedenciaFase</c> contra Postgres real
/// (UNI-REQ-0064, story #851): seed das seis arestas estruturais (CA-05), leitura
/// via <see cref="IPrecedenciaFaseReader"/>, UNIQUE parcial do par vivo e CHECKs de
/// domínio (formato, conjunto canônico, self-loop).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PrecedenciaFasePersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly (string Antecessora, string Sucessora)[] ArestasEsperadas =
    [
        ("INSCRICAO", "HOMOLOGACAO"),
        ("RESULTADO_PRELIMINAR", "RECURSOS"),
        ("RECURSOS", "RESULTADO_FINAL"),
        ("RESULTADO_FINAL", "HABILITACAO"),
        ("HABILITACAO", "MATRICULA"),
        ("HETEROIDENTIFICACAO", "HOMOLOGACAO_RESULTADO_FINAL"),
    ];

    private readonly ConfiguracaoDbFixture _fixture;

    public PrecedenciaFasePersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-05: o seed materializa as seis arestas canônicas e o reader devolve o grafo")]
    public async Task Seed_MaterializaArestasCanonicas()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new PrecedenciaFaseReader(ctx);

        IReadOnlyList<PrecedenciaFaseView> grafo = await reader.ListarVivasAsync();

        foreach ((string antecessora, string sucessora) in ArestasEsperadas)
        {
            grafo.Should().Contain(
                a => a.AntecessoraCodigo == antecessora && a.SucessoraCodigo == sucessora,
                $"a aresta {antecessora}→{sucessora} deve estar semeada");
        }
    }

    [Fact(DisplayName = "Criar persiste a aresta e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        PrecedenciaFase aresta = Aresta("ENSALAMENTO", "AVALIACAO");

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PrecedenciasFase.Add(aresta);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PrecedenciaFase persistida = await readCtx.PrecedenciasFase.SingleAsync(p => p.Id == aresta.Id);

        persistida.AntecessoraCodigo.Should().Be("ENSALAMENTO");
        persistida.SucessoraCodigo.Should().Be("AVALIACAO");
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();

        var reader = new PrecedenciaFaseReader(readCtx);
        PrecedenciaFaseView? view = await reader.ObterPorIdAsync(aresta.Id);
        view.Should().NotBeNull();
        view!.AntecessoraCodigo.Should().Be("ENSALAMENTO");
        view.SucessoraCodigo.Should().Be("AVALIACAO");
    }

    [Fact(DisplayName = "UNIQUE parcial do par rejeita segunda aresta viva com o mesmo par")]
    public async Task UniquePartial_Par_RejeitaDuplicataAtiva()
    {
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PrecedenciasFase.Add(Aresta("CLASSIFICACAO", "CHAMADA"));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.PrecedenciasFase.Add(Aresta("CLASSIFICACAO", "CHAMADA"));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_precedencia_fase_par_vivo");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do par")]
    public async Task SoftDelete_LiberaSlot()
    {
        PrecedenciaFase aresta = Aresta("LISTA_ESPERA", "CHAMADA");
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PrecedenciasFase.Add(aresta);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PrecedenciaFase tracked = await ctx.PrecedenciasFase.SingleAsync(p => p.Id == aresta.Id);
            ctx.PrecedenciasFase.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            PrecedenciaFase excluida = await ctx.PrecedenciasFase
                .IgnoreQueryFilters().SingleAsync(p => p.Id == aresta.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.PrecedenciasFase.Add(Aresta("LISTA_ESPERA", "CHAMADA"));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do par foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita self-loop via SQL cru")]
    public async Task Check_RejeitaSelfLoopViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.precedencia_fase (id, antecessora_codigo, sucessora_codigo, permite_sobreposicao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"INSCRICAO"}, {"INSCRICAO"}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK antecessora_codigo <> sucessora_codigo impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código de antecessora fora do conjunto canônico via SQL cru")]
    public async Task Check_RejeitaAntecessoraForaDoCanonicoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.precedencia_fase (id, antecessora_codigo, sucessora_codigo, permite_sobreposicao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"FASE_INVALIDA"}, {"HOMOLOGACAO"}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de conjunto canônico impede o INSERT direto");
    }

    private static PrecedenciaFase Aresta(string antecessora, string sucessora) =>
        PrecedenciaFase.Criar(antecessora, sucessora, false, []).Value!;
}
