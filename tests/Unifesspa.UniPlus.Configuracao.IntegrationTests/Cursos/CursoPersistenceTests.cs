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
        // Os cinco nomes compartilham um prefixo que os põe no fim da ordem
        // alfabética, formando um bloco contíguo: a tabela é estática dentro da
        // collection e acumula cursos de outros testes, cujos nomes começam por
        // "Engenharia". Sem isso, as linhas alheias se intercalariam ao bloco e as
        // âncoras deixariam de ser determinísticas.
        string marca = MarcaDeBloco();
        Curso[] cursos =
        [
            Novo(CodigoUnico(), nome: $"{marca} A"),
            Novo(CodigoUnico(), nome: $"{marca} B"),
            Novo(CodigoUnico(), nome: $"{marca} C"),
            Novo(CodigoUnico(), nome: $"{marca} D"),
            Novo(CodigoUnico(), nome: $"{marca} E"),
        ];

        // Inseridos fora de ordem, para a ordem de leitura não poder vir da ordem
        // de gravação.
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Cursos.AddRange([cursos[3], cursos[0], cursos[4], cursos[1], cursos[2]]);
            await ctx.SaveChangesAsync();
        }

        Guid[] ids = [.. cursos.Select(c => c.Id)];

        // A travessia parte das âncoras que a própria paginação emite, como faz um
        // cliente ao seguir o header Link — o teste não remonta a chave de
        // ordenação, cujo formato é interno ao cursor.
        List<Pagina> percorridas = await PercorrerTudoAsync();

        // A sequência global preserva a ordem alfabética do bloco, sem repetir nem
        // omitir, independentemente de onde ele caia entre as linhas das outras
        // baterias de teste desta collection.
        Guid[] doBloco =
        [
            .. percorridas
                .SelectMany(p => p.Itens)
                .Where(c => c.Nome.StartsWith(marca, StringComparison.Ordinal))
                .Select(c => c.Id),
        ];
        doBloco.Should().Equal(ids);

        // A volta por prev devolve exatamente a página anterior — mesma ordem
        // ascendente, mesmos itens.
        int indice = percorridas.FindIndex(p => p.Itens.Any(c => c.Id == ids[2]));
        indice.Should().BeGreaterThan(0, "o bloco fica no fim da ordem, então há páginas antes dele");

        Pagina comItemDoBloco = percorridas[indice];
        comItemDoBloco.Anterior.Should().NotBeNull("não é a primeira página da coleção");

        (IReadOnlyList<Curso> volta, _, (string SortKey, Guid Id)? voltaProx) =
            await PaginarAsync(comItemDoBloco.Anterior, PaginationDirection.Prev);

        volta.Select(c => c.Id).Should().Equal(percorridas[indice - 1].Itens.Select(c => c.Id));
        voltaProx!.Value.Id.Should().Be(volta[^1].Id, "a âncora de próximo é o último item da página");
    }

    private sealed record Pagina(
        IReadOnlyList<Curso> Itens,
        (string SortKey, Guid Id)? Anterior,
        (string SortKey, Guid Id)? Proximo);

    /// <summary>Percorre a listagem inteira seguindo as âncoras de próximo.</summary>
    private async Task<List<Pagina>> PercorrerTudoAsync()
    {
        List<Pagina> paginas = [];
        (string SortKey, Guid Id)? ancora = null;

        while (true)
        {
            (IReadOnlyList<Curso> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
                await PaginarAsync(ancora, PaginationDirection.Next);

            paginas.Add(new Pagina(itens, anterior, proximo));

            if (proximo is null)
            {
                return paginas;
            }

            ancora = proximo;
        }
    }

    private async Task<(IReadOnlyList<Curso> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        PaginarAsync((string SortKey, Guid Id)? ancora, PaginationDirection direction)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new CursoRepository(ctx);
        return await repository.ListarPaginadoAsync(
            ancora?.SortKey, ancora?.Id, limit: 2, direction, CancellationToken.None);
    }

    private static string MarcaDeBloco() =>
        $"ZZZ {Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static Curso Novo(
        string codigo,
        string? grupoAreaEnem = null,
        string nome = "Engenharia Civil") =>
        Curso.Criar(codigo, nome, "Bacharelado", "Graduação", grupoAreaEnem).Value!;

    private static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
