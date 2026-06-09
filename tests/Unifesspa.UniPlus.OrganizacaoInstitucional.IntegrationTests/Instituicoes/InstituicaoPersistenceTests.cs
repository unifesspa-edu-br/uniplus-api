namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Instituicoes;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

/// <summary>
/// Integração ponta-a-ponta da Story #585 contra Postgres real. Cobre os
/// cenários de persistência da <see cref="Instituicao"/> singleton: índice único
/// parcial sentinela (no máximo uma viva), soft-delete liberando o slot, audit
/// trail e FK intra-banco <c>unidade_raiz_id → unidade(id)</c>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class InstituicaoPersistenceTests : IClassFixture<InstituicaoDbFixture>
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private readonly InstituicaoDbFixture _fixture;

    public InstituicaoPersistenceTests(InstituicaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Insert persiste a Instituição e preenche created_by via AuditableInterceptor")]
    public async Task Insert_PersisteEPreencheAuditTrail()
    {
        // Estado compartilhado pela IClassFixture + singleton: limpa antes de
        // inserir para que a suíte não dependa da ordem de execução dos testes.
        await using OrganizacaoInstitucionalDbContext fresh = _fixture.CreateDbContext(AdminA);
        await LimparInstituicoesAsync(fresh);

        Instituicao instituicao = NovaInstituicao("9001", "INS-A");

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Instituicoes.Add(instituicao);
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Instituicao persistida = await readCtx.Instituicoes.SingleAsync(i => i.Id == instituicao.Id);

        persistida.CodigoEmec.Should().Be("9001");
        persistida.IsDeleted.Should().BeFalse();
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.UpdatedBy.Should().BeNull("apenas criação — sem update ainda");
    }

    [Fact(DisplayName = "Índice único parcial rejeita uma segunda Instituição viva (singleton — CA-02)")]
    public async Task Singleton_SegundaInstituicaoVivaRejeitada()
    {
        await using OrganizacaoInstitucionalDbContext fresh = _fixture.CreateDbContext(AdminA);
        await LimparInstituicoesAsync(fresh);

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Instituicoes.Add(NovaInstituicao("9100", "INS-1"));
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Instituicoes.Add(NovaInstituicao("9101", "INS-2"));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsSingletonConflict) em
        // JaExisteInstituicaoViva/409: SqlState 23505 + nome do índice sentinela.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        var pg = ex.InnerException as Npgsql.PostgresException;
        pg.Should().NotBeNull();
        pg!.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_instituicao_singleton_vivo");
    }

    [Fact(DisplayName = "CHECK constraint impede gravar a sentinela como false (singleton constante no banco)")]
    public async Task CheckConstraint_ImpedeSentinelaFalse()
    {
        await using OrganizacaoInstitucionalDbContext fresh = _fixture.CreateDbContext(AdminA);
        await LimparInstituicoesAsync(fresh);

        Instituicao instituicao = NovaInstituicao("9500", "INS-CK");
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Instituicoes.Add(instituicao);
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext rawCtx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await rawCtx.Database.ExecuteSqlAsync(
            $"UPDATE instituicao SET registro_vivo_sentinela = false WHERE id = {instituicao.Id}");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "a CHECK constraint ck_instituicao_singleton_sentinela só admite a sentinela true");
    }

    [Fact(DisplayName = "Soft-delete liberta o slot singleton — nova Instituição é aceita após exclusão (CA-05)")]
    public async Task Singleton_AposSoftDeleteNovaAceita()
    {
        await using OrganizacaoInstitucionalDbContext fresh = _fixture.CreateDbContext(AdminA);
        await LimparInstituicoesAsync(fresh);

        Instituicao primeira = NovaInstituicao("9200", "INS-DEL");
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Instituicoes.Add(primeira);
            await ctx.SaveChangesAsync();
        }

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            Instituicao tracked = await ctx.Instituicoes.SingleAsync(i => i.Id == primeira.Id);
            ctx.Instituicoes.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        // O registro removido permanece na trilha (IgnoreQueryFilters) com a auditoria preenchida.
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            Instituicao removida = await ctx.Instituicoes
                .IgnoreQueryFilters()
                .SingleAsync(i => i.Id == primeira.Id);
            removida.IsDeleted.Should().BeTrue();
            removida.DeletedAt.Should().NotBeNull();
            removida.DeletedBy.Should().Be(AdminB);
        }

        // Nova Instituição viva é aceita — o índice único parcial só vê registros vivos.
        await using OrganizacaoInstitucionalDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.Instituicoes.Add(NovaInstituicao("9201", "INS-NOVA"));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();

        await act.Should().NotThrowAsync("slot liberado pelo soft-delete");
    }

    [Fact(DisplayName = "ExisteAlgumaVivaAsync reflete registros vivos e ignora os soft-deleted (CA-05)")]
    public async Task ExisteAlgumaVivaAsync_RefleteRegistrosVivos()
    {
        await using OrganizacaoInstitucionalDbContext fresh = _fixture.CreateDbContext(AdminA);
        await LimparInstituicoesAsync(fresh);

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            var repoVazio = new InstituicaoRepository(ctx);
            (await repoVazio.ExisteAlgumaVivaAsync(CancellationToken.None)).Should().BeFalse();
        }

        Instituicao instituicao = NovaInstituicao("9300", "INS-EX");
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Instituicoes.Add(instituicao);
            await ctx.SaveChangesAsync();
        }

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            var repoComUma = new InstituicaoRepository(ctx);
            (await repoComUma.ExisteAlgumaVivaAsync(CancellationToken.None)).Should().BeTrue();
        }

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            Instituicao tracked = await ctx.Instituicoes.SingleAsync(i => i.Id == instituicao.Id);
            ctx.Instituicoes.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            var repoPosRemocao = new InstituicaoRepository(ctx);
            (await repoPosRemocao.ExisteAlgumaVivaAsync(CancellationToken.None))
                .Should().BeFalse("registro removido não conta para o limite singleton");
        }
    }

    [Fact(DisplayName = "FK unidade_raiz_id aceita vínculo com Unidade do tipo reitoria (CA-04)")]
    public async Task Fk_UnidadeRaizReitoria_Aceita()
    {
        await using OrganizacaoInstitucionalDbContext fresh = _fixture.CreateDbContext(AdminA);
        await LimparInstituicoesAsync(fresh);

        Unidade reitoria = NovaReitoria("reitoria-fk", "RFK", "RFK001");
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(reitoria);
            await ctx.SaveChangesAsync();
        }

        Instituicao instituicao = NovaInstituicao("9400", "INS-FK", reitoria.Id);
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Instituicoes.Add(instituicao);
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Instituicao persistida = await readCtx.Instituicoes.SingleAsync(i => i.Id == instituicao.Id);
        persistida.UnidadeRaizId.Should().Be(reitoria.Id);

        var repository = new InstituicaoRepository(readCtx);
        (await repository.ExisteComUnidadeRaizAsync(reitoria.Id, CancellationToken.None))
            .Should().BeTrue("a Instituição referencia esta Unidade como raiz");
        (await repository.ExisteComUnidadeRaizAsync(Guid.CreateVersion7(), CancellationToken.None))
            .Should().BeFalse("nenhuma Instituição referencia uma Unidade aleatória");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static async Task LimparInstituicoesAsync(OrganizacaoInstitucionalDbContext ctx)
    {
        // Cada teste precisa de um ponto de partida sem Instituição viva — o
        // singleton compartilha o slot único entre todos os testes da fixture.
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM instituicao");
    }

    private static Instituicao NovaInstituicao(string codigoEmec, string sigla, Guid? unidadeRaizId = null) =>
        Instituicao.Criar(
            codigoEmec,
            $"Instituição {sigla}",
            sigla,
            "Universidade",
            "Pública Federal",
            cnpj: null,
            mantenedora: null,
            codigoMantenedoraEmec: null,
            situacao: null,
            atoCredenciamento: null,
            atoRecredenciamento: null,
            conceitoInstitucional: null,
            igc: null,
            website: null,
            enderecoSede: null,
            municipioSede: null,
            unidadeRaizId).Value!;

    private static Unidade NovaReitoria(string slug, string sigla, string codigo) =>
        Unidade.Criar(
            nome: "Reitoria",
            alias: null,
            slug: Slug.From(slug).Value!,
            sigla: sigla,
            codigo: codigo,
            unidadeSuperiorId: null,
            tipo: TipoUnidade.Reitoria,
            unidadeAcademica: false,
            vigenciaInicio: DataInicio,
            vigenciaFim: null,
            origem: OrigemUnidade.CriadoNoUniPlus).Value!;
}
