namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposBanca;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta do TipoBanca contra Postgres real (UNI-REQ-0064):
/// persistência, UNIQUE parcial do código vivo, liberação do slot por soft-delete,
/// CHECKs de domínio (formato, conjunto canônico) e leitura cross-módulo. Cada teste
/// usa um código canônico distinto (o conjunto é fechado — quatro bancas).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoBancaPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoBancaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        TipoBanca banca = Banca("BANCA_ANALISE_DOCUMENTAL", "Banca de análise documental", "Homologação");

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposBanca.Add(banca);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoBanca persistida = await readCtx.TiposBanca.SingleAsync(b => b.Id == banca.Id);

        persistida.Codigo.Valor.Should().Be("BANCA_ANALISE_DOCUMENTAL");
        persistida.FaseTipica.Should().Be("Homologação");
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();

        var reader = new TipoBancaReader(readCtx);
        TipoBancaView? view = await reader.ObterPorIdAsync(banca.Id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be("BANCA_ANALISE_DOCUMENTAL");
        view.FaseTipica.Should().Be("Homologação");
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segunda banca viva com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposBanca.Add(Banca("BANCA_ENTREVISTA", "Banca de entrevista", null));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.TiposBanca.Add(Banca("BANCA_ENTREVISTA", "Banca de entrevista", null));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_tipo_banca_codigo_vivo");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_LiberaSlot()
    {
        TipoBanca banca = Banca("BANCA_CORRECAO_REDACOES", "Banca de correção de redações", null);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposBanca.Add(banca);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TipoBanca tracked = await ctx.TiposBanca.SingleAsync(b => b.Id == banca.Id);
            ctx.TiposBanca.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            TipoBanca excluida = await ctx.TiposBanca
                .IgnoreQueryFilters().SingleAsync(b => b.Id == banca.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.TiposBanca.Add(Banca("BANCA_CORRECAO_REDACOES", "Banca de correção de redações", null));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do conjunto canônico via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoCanonicoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_banca (id, codigo, nome, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"BANCA_LOGISTICA"}, {"Banca"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de conjunto canônico impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do formato via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoFormatoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_banca (id, codigo, nome, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"banca-entrevista"}, {"Banca"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK codigo ~ '^[A-Z_]+$' impede o INSERT direto");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync exclui bancas soft-deleted")]
    public async Task ListarVivos_ExcluiSoftDeleted()
    {
        TipoBanca banca = Banca("BANCA_ANALISE_RECURSOS", "Banca de análise de recursos", null);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposBanca.Add(banca);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CodigoBanca vo = CodigoBanca.Criar("BANCA_ANALISE_RECURSOS").Value!;
            TipoBanca aExcluir = await ctx.TiposBanca.SingleAsync(b => b.Codigo == vo);
            ctx.TiposBanca.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoBancaReader(readCtx);
        IReadOnlyList<TipoBancaView> todos = await reader.ListarVivosAsync();

        string[] codigos = [.. todos.Select(v => v.Codigo)];
        codigos.Should().BeInAscendingOrder("o leitor ordena por código ascendente");
        codigos.Should().NotContain("BANCA_ANALISE_RECURSOS", "a banca soft-deleted não aparece no leitor de vivos");
    }

    private static TipoBanca Banca(string codigo, string nome, string? faseTipica) =>
        TipoBanca.Criar(codigo, nome, faseTipica, null).Value!;
}
