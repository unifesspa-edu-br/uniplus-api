namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta da FaseCanonica contra Postgres real (UNI-REQ-0064):
/// persistência, UNIQUE parcial do código vivo, liberação do slot por soft-delete,
/// CHECKs de domínio (formato, conjunto canônico, dono típico, coerência de
/// agrupar etapas e complementação) e leitura cross-módulo ordenada por código.
/// Cada teste usa códigos canônicos distintos (o conjunto é fechado).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class FaseCanonicaPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public FaseCanonicaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        FaseCanonica fase = Fase("INSCRICAO", "Inscrição", "CEPS");

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.FasesCanonicas.Add(fase);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        FaseCanonica persistida = await readCtx.FasesCanonicas.SingleAsync(f => f.Id == fase.Id);

        persistida.Codigo.Valor.Should().Be("INSCRICAO");
        persistida.DonoTipico.Should().Be(DonoTipico.Ceps);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();

        var reader = new FaseCanonicaReader(readCtx);
        FaseCanonicaView? view = await reader.ObterPorIdAsync(fase.Id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be("INSCRICAO");
        view.DonoTipico.Should().Be("CEPS");
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segunda fase viva com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.FasesCanonicas.Add(Fase("ENSALAMENTO", "Ensalamento", "CEPS"));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.FasesCanonicas.Add(Fase("ENSALAMENTO", "Ensalamento", "CEPS"));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_fase_canonica_codigo_vivo");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_LiberaSlot()
    {
        FaseCanonica fase = Fase("CLASSIFICACAO", "Classificação", "CEPS");
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.FasesCanonicas.Add(fase);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            FaseCanonica tracked = await ctx.FasesCanonicas.SingleAsync(f => f.Id == fase.Id);
            ctx.FasesCanonicas.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            FaseCanonica excluida = await ctx.FasesCanonicas
                .IgnoreQueryFilters().SingleAsync(f => f.Id == fase.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.FasesCanonicas.Add(Fase("CLASSIFICACAO", "Classificação", "CEPS"));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do conjunto canônico via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoCanonicoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.fase_canonica (id, codigo, nome, dono_tipico, agrupa_etapas, permite_complementacao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"FASE_INVALIDA"}, {"Fase"}, {"CEPS"}, {false}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de conjunto canônico impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do formato via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoFormatoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.fase_canonica (id, codigo, nome, dono_tipico, agrupa_etapas, permite_complementacao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"lista-espera"}, {"Fase"}, {"CEPS"}, {false}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK codigo ~ '^[A-Z_]+$' impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita dono típico fora do domínio via SQL cru")]
    public async Task Check_RejeitaDonoTipicoForaDoDominioViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.fase_canonica (id, codigo, nome, dono_tipico, agrupa_etapas, permite_complementacao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"RESULTADO_PRELIMINAR"}, {"Resultado preliminar"}, {"DTI"}, {false}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de domínio de dono_tipico impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita agrupar etapas fora da avaliação via SQL cru")]
    public async Task Check_RejeitaAgrupaEtapasForaDaAvaliacaoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.fase_canonica (id, codigo, nome, dono_tipico, agrupa_etapas, permite_complementacao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"HABILITACAO"}, {"Habilitação"}, {"MEC"}, {true}, {false}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência agrupa_etapas ⇒ avaliação impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita complementação em fase vedada via SQL cru")]
    public async Task Check_RejeitaComplementacaoFaseVedadaViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.fase_canonica (id, codigo, nome, dono_tipico, agrupa_etapas, permite_complementacao, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"RESULTADO_FINAL"}, {"Resultado final"}, {"CEPS"}, {false}, {true}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência permite_complementacao ⇒ fases permitidas impede o INSERT direto");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por código ascendente e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorCodigoEExcluiSoftDeleted()
    {
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.FasesCanonicas.Add(Fase("MATRICULA", "Matrícula", "CRCA"));
            ctx.FasesCanonicas.Add(Fase("HETEROIDENTIFICACAO", "Heteroidentificação", "CEPS"));
            ctx.FasesCanonicas.Add(Fase("CHAMADA", "Chamada", "CEPS"));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CodigoFase voExcluido = CodigoFase.Criar("CHAMADA").Value!;
            FaseCanonica aExcluir = await ctx.FasesCanonicas.SingleAsync(f => f.Codigo == voExcluido);
            ctx.FasesCanonicas.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new FaseCanonicaReader(readCtx);
        IReadOnlyList<FaseCanonicaView> todos = await reader.ListarVivosAsync();

        string[] codigos = [.. todos.Select(v => v.Codigo)];
        codigos.Should().BeInAscendingOrder("o leitor ordena por código ascendente");
        codigos.Should().Contain(["HETEROIDENTIFICACAO", "MATRICULA"]);
        codigos.Should().NotContain("CHAMADA", "a fase soft-deleted não aparece no leitor de vivos");
    }

    private static FaseCanonica Fase(string codigo, string nome, string dono) =>
        FaseCanonica.Criar(codigo, nome, null, dono, false, false, null).Value!;
}
