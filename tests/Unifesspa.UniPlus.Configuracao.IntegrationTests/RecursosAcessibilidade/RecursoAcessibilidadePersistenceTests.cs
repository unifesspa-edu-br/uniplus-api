namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.RecursosAcessibilidade;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta do RecursoAcessibilidade contra Postgres real
/// (UNI-REQ-0012): persistência, UNIQUE parcial do nome vivo, liberação do slot por
/// soft-delete e leitura cross-módulo ordenada por nome.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class RecursoAcessibilidadePersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public RecursoAcessibilidadePersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string nome = NomeUnico();
        RecursoAcessibilidade recurso = Novo(nome);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.RecursosAcessibilidade.Add(recurso);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        RecursoAcessibilidade persistido = await readCtx.RecursosAcessibilidade.SingleAsync(r => r.Id == recurso.Id);

        persistido.Nome.Should().Be(nome);
        persistido.CreatedBy.Should().Be(AdminA);
        persistido.IsDeleted.Should().BeFalse();

        var reader = new RecursoAcessibilidadeReader(readCtx);
        RecursoAcessibilidadeView? view = await reader.ObterPorIdAsync(recurso.Id);
        view.Should().NotBeNull();
        view!.Nome.Should().Be(nome);
    }

    [Fact(DisplayName = "UNIQUE parcial do nome rejeita segundo recurso vivo com mesmo nome")]
    public async Task UniquePartial_Nome_RejeitaDuplicataAtiva()
    {
        string nome = NomeUnico();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.RecursosAcessibilidade.Add(Novo(nome));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.RecursosAcessibilidade.Add(Novo(nome));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsNomeConflict) em
        // NomeJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_recurso_acessibilidade_nome_vivo");
    }

    [Fact(DisplayName = "Nome distinto é aceito")]
    public async Task NomeDistinto_Aceita()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.RecursosAcessibilidade.Add(Novo(NomeUnico()));
        ctx.RecursosAcessibilidade.Add(Novo(NomeUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("os nomes são distintos");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do nome")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string nome = NomeUnico();
        RecursoAcessibilidade recurso = Novo(nome);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.RecursosAcessibilidade.Add(recurso);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            RecursoAcessibilidade tracked = await ctx.RecursosAcessibilidade.SingleAsync(r => r.Id == recurso.Id);
            ctx.RecursosAcessibilidade.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            RecursoAcessibilidade excluido = await ctx.RecursosAcessibilidade
                .IgnoreQueryFilters().SingleAsync(r => r.Id == recurso.Id);
            excluido.IsDeleted.Should().BeTrue();
            excluido.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.RecursosAcessibilidade.Add(Novo(nome));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do nome foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por nome e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorNomeEExcluiSoftDeleted()
    {
        string prefixo = $"REC_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        string nomeA = $"{prefixo}_A";
        string nomeB = $"{prefixo}_B";
        string nomeExcluido = $"{prefixo}_D";

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.RecursosAcessibilidade.Add(Novo(nomeB));
            ctx.RecursosAcessibilidade.Add(Novo(nomeA));
            ctx.RecursosAcessibilidade.Add(Novo(nomeExcluido));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            RecursoAcessibilidade aExcluir = await ctx.RecursosAcessibilidade.SingleAsync(r => r.Nome == nomeExcluido);
            ctx.RecursosAcessibilidade.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new RecursoAcessibilidadeReader(readCtx);
        IReadOnlyList<RecursoAcessibilidadeView> todos = await reader.ListarVivosAsync();

        string[] meus = [.. todos
            .Select(v => v.Nome)
            .Where(n => n.StartsWith(prefixo, StringComparison.Ordinal))];

        meus.Should().Equal([nomeA, nomeB]);
    }

    private static RecursoAcessibilidade Novo(string nome) =>
        RecursoAcessibilidade.Criar(nome, "Recurso de acessibilidade").Value!;

    private static string NomeUnico() => $"Recurso {Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
