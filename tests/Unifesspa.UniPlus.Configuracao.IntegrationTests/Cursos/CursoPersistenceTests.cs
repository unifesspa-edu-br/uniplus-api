namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Integração ponta-a-ponta do Curso contra Postgres real (story #588):
/// persistência (com e sem grupo de área do ENEM), UNIQUE parcial do código
/// vivo, liberação do slot por soft-delete e CHECK null-safe do domínio fechado
/// do grupo de área do ENEM.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CursoPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public CursoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos (com grupo de área do ENEM) e carimba a auditoria")]
    public async Task Insert_ComGrupoAreaEnem_Persiste()
    {
        string codigo = CodigoUnico();
        Curso curso = Novo(codigo, grupoAreaEnem: GrupoCurso.Tecnologica);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Cursos.Add(curso);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Curso persistido = await readCtx.Cursos.SingleAsync(c => c.Id == curso.Id);

        persistido.Codigo.Should().Be(codigo);
        persistido.Nome.Should().Be("Engenharia Civil");
        persistido.Grau.Should().Be("Bacharelado");
        persistido.NivelEnsino.Should().Be("Graduação");
        persistido.GrupoAreaEnem!.Valor.Should().Be(GrupoCurso.Tecnologica);
        persistido.CreatedBy.Should().Be(AdminA);
        persistido.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar sem grupo de área do ENEM persiste a coluna nula e reidrata como nulo")]
    public async Task Insert_SemGrupoAreaEnem_PersisteNulo()
    {
        Curso curso = Novo(CodigoUnico(), grupoAreaEnem: null);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Cursos.Add(curso);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Curso persistido = await readCtx.Cursos.SingleAsync(c => c.Id == curso.Id);

        persistido.GrupoAreaEnem.Should().BeNull("nem todo curso classifica por área do ENEM");
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segundo curso vivo com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Cursos.Add(Novo(codigo));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Cursos.Add(Novo(codigo));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsCodigoConflict) em
        // CodigoJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_curso_codigo_vivo");
    }

    [Fact(DisplayName = "Código distinto é aceito")]
    public async Task CodigoDistinto_Aceita()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Cursos.Add(Novo(CodigoUnico()));
        ctx.Cursos.Add(Novo(CodigoUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("os códigos são distintos");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string codigo = CodigoUnico();
        Curso curso = Novo(codigo);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Cursos.Add(curso);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            Curso tracked = await ctx.Cursos.SingleAsync(c => c.Id == curso.Id);
            ctx.Cursos.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            Curso excluido = await ctx.Cursos
                .IgnoreQueryFilters().SingleAsync(c => c.Id == curso.Id);
            excluido.IsDeleted.Should().BeTrue();
            excluido.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.Cursos.Add(Novo(codigo));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita grupo de área do ENEM fora do domínio via SQL cru")]
    public async Task Check_RejeitaGrupoForaDoDominioViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.curso (id, codigo, nome, grau, nivel_ensino, grupo_area_enem, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"Bacharelado"}, {"Graduação"}, {"Exatas"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de domínio do grupo de área do ENEM impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco aceita grupo de área do ENEM nulo via SQL cru (null-safe)")]
    public async Task Check_AceitaGrupoNuloViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        // A coluna grupo_area_enem é omitida de propósito: fica NULL e o CHECK
        // null-safe não pode rejeitar o INSERT.
        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.curso (id, codigo, nome, grau, nivel_ensino, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"Bacharelado"}, {"Graduação"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().NotThrowAsync("a coluna é opcional e o CHECK é null-safe");
    }

    [Fact(DisplayName = "Navegação bidirecional do cursor: prev volta exatamente à página anterior, com flags coerentes")]
    public async Task Navegacao_Bidirecional_PrevVoltaPaginaAnterior()
    {
        // 5 cursos criados agora: o prefixo temporal do Guid v7 garante que o BLOCO
        // fica no fim da tabela em ASC por Id — âncora determinística mesmo com
        // linhas pré-existentes de outros testes da collection (sequencial, tabela
        // estática). Dentro do mesmo milissegundo os bits aleatórios permutam a
        // ordem interna do bloco, então os ids são ordenados como o Postgres ordena
        // uuid (byte a byte = ordem lexicográfica do hex canônico).
        Curso[] cursos = [Novo(CodigoUnico()), Novo(CodigoUnico()), Novo(CodigoUnico()), Novo(CodigoUnico()), Novo(CodigoUnico())];
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Cursos.AddRange(cursos);
            await ctx.SaveChangesAsync();
        }

        Guid[] ids = [.. cursos.Select(c => c.Id).OrderBy(id => id.ToString(), StringComparer.Ordinal)];

        // Página 1 (forward a partir de ids[0], limit 2): [1,2]; há anterior (ids[0]) e próximo.
        (IReadOnlyList<Curso> p1, Guid? p1Ant, Guid? p1Prox) = await PaginarAsync(afterId: ids[0], PaginationDirection.Next);
        p1.Select(c => c.Id).Should().Equal(ids[1], ids[2]);
        p1Ant.Should().Be(ids[1]);
        p1Prox.Should().Be(ids[2]);

        // Página 2 (forward a partir do próximo da p1): [3,4]; última página → sem próximo.
        (IReadOnlyList<Curso> p2, Guid? p2Ant, Guid? p2Prox) = await PaginarAsync(afterId: p1Prox, PaginationDirection.Next);
        p2.Select(c => c.Id).Should().Equal(ids[3], ids[4]);
        p2Ant.Should().Be(ids[3]);
        p2Prox.Should().BeNull("ids[4] é a linha mais recente da tabela (Guid v7)");

        // Backward a partir do anterior da p2: volta exatamente à página 1 em ASC.
        (IReadOnlyList<Curso> volta, Guid? voltaAnt, Guid? voltaProx) = await PaginarAsync(afterId: p2Ant, PaginationDirection.Prev);
        volta.Select(c => c.Id).Should().Equal(ids[1], ids[2]);
        voltaAnt.Should().Be(ids[1], "ainda há linhas antes de ids[1] (ao menos ids[0])");
        voltaProx.Should().Be(ids[2]);
    }

    private async Task<(IReadOnlyList<Curso> Itens, Guid? Anterior, Guid? Proximo)> PaginarAsync(
        Guid? afterId,
        PaginationDirection direction)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new CursoRepository(ctx);
        return await repository.ListarPaginadoAsync(afterId, limit: 2, direction, CancellationToken.None);
    }

    private static Curso Novo(
        string codigo,
        string? grupoAreaEnem = null) =>
        Curso.Criar(codigo, "Engenharia Civil", "Bacharelado", "Graduação", grupoAreaEnem).Value!;

    private static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
