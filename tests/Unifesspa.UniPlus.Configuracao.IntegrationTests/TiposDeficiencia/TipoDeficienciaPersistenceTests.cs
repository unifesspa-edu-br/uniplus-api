namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta do TipoDeficiencia contra Postgres real (UNI-REQ-0012):
/// persistência, UNIQUE parcial do nome vivo, liberação do slot por soft-delete e
/// leitura cross-módulo ordenada por nome.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoDeficienciaPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoDeficienciaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string nome = NomeUnico();
        TipoDeficiencia tipo = Novo(nome);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDeficiencia.Add(tipo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoDeficiencia persistido = await readCtx.TiposDeficiencia.SingleAsync(t => t.Id == tipo.Id);

        persistido.Nome.Should().Be(nome);
        persistido.CreatedBy.Should().Be(AdminA);
        persistido.IsDeleted.Should().BeFalse();

        var reader = new TipoDeficienciaReader(readCtx);
        TipoDeficienciaView? view = await reader.ObterPorIdAsync(tipo.Id);
        view.Should().NotBeNull();
        view!.Nome.Should().Be(nome);
    }

    [Fact(DisplayName = "UNIQUE parcial do nome rejeita segundo tipo vivo com mesmo nome")]
    public async Task UniquePartial_Nome_RejeitaDuplicataAtiva()
    {
        string nome = NomeUnico();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDeficiencia.Add(Novo(nome));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.TiposDeficiencia.Add(Novo(nome));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsNomeConflict) em
        // NomeJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_tipo_deficiencia_nome_vivo");
    }

    [Fact(DisplayName = "Nome distinto é aceito")]
    public async Task NomeDistinto_Aceita()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.TiposDeficiencia.Add(Novo(NomeUnico()));
        ctx.TiposDeficiencia.Add(Novo(NomeUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("os nomes são distintos");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do nome")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string nome = NomeUnico();
        TipoDeficiencia tipo = Novo(nome);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDeficiencia.Add(tipo);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TipoDeficiencia tracked = await ctx.TiposDeficiencia.SingleAsync(t => t.Id == tipo.Id);
            ctx.TiposDeficiencia.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            TipoDeficiencia excluido = await ctx.TiposDeficiencia
                .IgnoreQueryFilters().SingleAsync(t => t.Id == tipo.Id);
            excluido.IsDeleted.Should().BeTrue();
            excluido.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.TiposDeficiencia.Add(Novo(nome));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do nome foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por nome e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorNomeEExcluiSoftDeleted()
    {
        string prefixo = $"DEF_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        string nomeA = $"{prefixo}_A";
        string nomeB = $"{prefixo}_B";
        string nomeExcluido = $"{prefixo}_D";

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDeficiencia.Add(Novo(nomeB));
            ctx.TiposDeficiencia.Add(Novo(nomeA));
            ctx.TiposDeficiencia.Add(Novo(nomeExcluido));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TipoDeficiencia aExcluir = await ctx.TiposDeficiencia.SingleAsync(t => t.Nome == nomeExcluido);
            ctx.TiposDeficiencia.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoDeficienciaReader(readCtx);
        IReadOnlyList<TipoDeficienciaView> todos = await reader.ListarVivosAsync();

        string[] meus = [.. todos
            .Select(v => v.Nome)
            .Where(n => n.StartsWith(prefixo, StringComparison.Ordinal))];

        meus.Should().Equal([nomeA, nomeB]);
    }

    private static TipoDeficiencia Novo(string nome) =>
        TipoDeficiencia.Criar(nome, "Descrição de teste").Value!;

    private static string NomeUnico() => $"DEF_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
