namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CategoriasDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta da CategoriaDocumento contra Postgres real
/// (UNI-REQ-0013): persistência dos campos e da auditoria de ator, UNIQUE parcial
/// do código vivo, liberação do slot por soft-delete e CHECKs de formato do código
/// e de ordem não negativa contra inserts crus.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CategoriaDocumentoPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public CategoriaDocumentoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos, a ordem e o ator da criação")]
    public async Task Insert_PersisteCamposEAuditoria()
    {
        string codigo = CodigoUnico();
        CategoriaDocumento categoria = Nova(codigo, ordem: 30);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CategoriasDocumento.Add(categoria);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        CategoriaDocumento persistida = await readCtx.CategoriasDocumento.SingleAsync(c => c.Id == categoria.Id);

        persistida.Codigo.Valor.Should().Be(codigo);
        persistida.Nome.Should().Be("Documento processual");
        persistida.Ordem.Should().Be(30);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segunda categoria viva com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CategoriasDocumento.Add(Nova(codigo));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.CategoriasDocumento.Add(Nova(codigo));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsCodigoConflict) em
        // CodigoJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_categoria_documento_codigo_vivo");
    }

    [Fact(DisplayName = "Código distinto é aceito")]
    public async Task CodigoDistinto_Aceita()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.CategoriasDocumento.Add(Nova(CodigoUnico()));
        ctx.CategoriasDocumento.Add(Nova(CodigoUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("os códigos são distintos");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string codigo = CodigoUnico();
        CategoriaDocumento categoria = Nova(codigo);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CategoriasDocumento.Add(categoria);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CategoriaDocumento tracked = await ctx.CategoriasDocumento.SingleAsync(c => c.Id == categoria.Id);
            ctx.CategoriasDocumento.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            CategoriaDocumento excluida = await ctx.CategoriasDocumento
                .IgnoreQueryFilters().SingleAsync(c => c.Id == categoria.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.CategoriasDocumento.Add(Nova(codigo));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do formato fechado via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoFormatoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.categoria_documento (id, codigo, nome, ordem, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"renda"}, {"Renda"}, {0}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de formato do código (UPPER_SNAKE iniciando por letra) impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita ordem negativa via SQL cru")]
    public async Task Check_RejeitaOrdemNegativaViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.categoria_documento (id, codigo, nome, ordem, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"Renda"}, {-1}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "a ordem de exibição é posição no catálogo e não admite valor negativo");
    }

    private static CategoriaDocumento Nova(string codigo, string nome = "Documento processual", int ordem = 0) =>
        CategoriaDocumento.Criar(codigo, nome, null, ordem).Value!;

    private static string CodigoUnico() => $"CAT_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
