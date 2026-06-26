namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

/// <summary>
/// Integração ponta-a-ponta da Story #586 contra Postgres real. Cobre os
/// cenários de persistência da <see cref="Unidade"/>:
/// UNIQUE parcial slug/sigla/codigo, soft-delete liberando slot único,
/// histórico de identificadores, audit trail e FK hierárquica.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class UnidadePersistenceTests : IClassFixture<UnidadeDbFixture>
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private readonly UnidadeDbFixture _fixture;

    public UnidadePersistenceTests(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Insert persiste Unidade e cria 3 entradas de histórico (Slug, Sigla, Codigo)")]
    public async Task Insert_PersistUnidadeECriaHistoricoDeIdentificadores()
    {
        Unidade unidade = NovaUnidade("insert-basico", "CEPS-INS", "INS001");

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(unidade);
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext readCtx = _fixture.CreateDbContext(userId: null);

        Unidade persistida = await readCtx.Unidades
            .Include(u => u.Historico)
            .SingleAsync(u => u.Id == unidade.Id);

        persistida.Slug.Should().Be(Slug.From("insert-basico").Value!);
        persistida.Sigla.Should().Be("CEPS-INS");
        persistida.Codigo.Should().Be("INS001");
        persistida.IsDeleted.Should().BeFalse();

        persistida.Historico.Should().HaveCount(3);
        persistida.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Slug && h.Valor == "insert-basico" && h.VigenciaFim == null);
        persistida.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Sigla && h.Valor == "CEPS-INS" && h.VigenciaFim == null);
        persistida.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Codigo && h.Valor == "INS001" && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Update de identificador fecha histórico atual e insere nova entrada")]
    public async Task Update_Identificador_InsereNovaEntradaHistorico()
    {
        Unidade unidade = NovaUnidade(
            "hist-update",
            "HUPD",
            "HUP001",
            alias: "Alias original");

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(unidade);
            await ctx.SaveChangesAsync();
        }

        DateOnly dataMudanca = DataInicio.AddDays(10);

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            var repository = new UnidadeRepository(ctx);
            Unidade tracked = (await repository.ObterPorIdAsync(unidade.Id, CancellationToken.None))!;

            Result atualizarResult = tracked.Atualizar(
                tracked.Nome,
                "Alias atualizado",
                tracked.Slug,
                tracked.Sigla,
                tracked.Codigo,
                tracked.UnidadeSuperiorId,
                tracked.Tipo,
                tracked.UnidadeAcademica,
                tracked.VigenciaFim,
                dataMudanca,
                "ajuste de alias");

            atualizarResult.IsSuccess.Should().BeTrue();

            Func<Task> act = async () => await ctx.SaveChangesAsync();

            await act.Should().NotThrowAsync(
                "a nova entrada de histórico deve ser inserida, não tratada como update de linha existente");
        }

        await using OrganizacaoInstitucionalDbContext readCtx = _fixture.CreateDbContext(userId: null);

        Unidade persistida = await readCtx.Unidades.SingleAsync(u => u.Id == unidade.Id);
        persistida.Alias.Should().Be("Alias atualizado");
        persistida.UpdatedBy.Should().Be(AdminB);

        List<UnidadeIdentificadorHistorico> historicoAlias = await readCtx.UnidadesIdentificadoresHistorico
            .Where(h => h.UnidadeId == unidade.Id && h.TipoIdentificador == TipoIdentificador.Alias)
            .OrderBy(h => h.VigenciaInicio)
            .ToListAsync();

        historicoAlias.Should().HaveCount(2);
        historicoAlias[0].Valor.Should().Be("Alias original");
        historicoAlias[0].VigenciaFim.Should().Be(dataMudanca);
        historicoAlias[1].Valor.Should().Be("Alias atualizado");
        historicoAlias[1].VigenciaFim.Should().BeNull();
        historicoAlias[1].MotivoMudanca.Should().Be("ajuste de alias");
    }

    [Fact(DisplayName = "AuditableInterceptor preenche created_by com o UserId do contexto")]
    public async Task Insert_PreencheCreatedByViaAuditableInterceptor()
    {
        Unidade unidade = NovaUnidade("audit-trail", "AUDIT", "AUD001");

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(unidade);
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext readCtx = _fixture.CreateDbContext(userId: null);

        Unidade persistida = await readCtx.Unidades
            .SingleAsync(u => u.Id == unidade.Id);

        persistida.CreatedBy.Should().Be(AdminA);
        persistida.UpdatedBy.Should().BeNull("apenas criação — sem update ainda");
    }

    [Fact(DisplayName = "UNIQUE parcial (slug) rejeita segunda Unidade ativa com mesmo slug")]
    public async Task UniquePartial_Slug_RejeitaDuplicataAtiva()
    {
        Unidade primeira = NovaUnidade("slug-unico", "SU01", "SUC001");
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(primeira);
            await ctx.SaveChangesAsync();
        }

        Unidade segunda = NovaUnidade("slug-unico", "SU02", "SUC002");
        await using OrganizacaoInstitucionalDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Unidades.Add(segunda);

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException(typeof(Npgsql.PostgresException));
    }

    [Fact(DisplayName = "UNIQUE parcial (sigla) rejeita segunda Unidade ativa com mesma sigla")]
    public async Task UniquePartial_Sigla_RejeitaDuplicataAtiva()
    {
        Unidade primeira = NovaUnidade("sigla-unica-a", "SIGDUP", "SGDUP001");
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(primeira);
            await ctx.SaveChangesAsync();
        }

        Unidade segunda = NovaUnidade("sigla-unica-b", "SIGDUP", "SGDUP002");
        await using OrganizacaoInstitucionalDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Unidades.Add(segunda);

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException(typeof(Npgsql.PostgresException));
    }

    [Fact(DisplayName = "Soft-delete liberta o slot da UNIQUE parcial — novo slug é aceito após exclusão")]
    public async Task SoftDelete_LibertaUniquePartialSlug()
    {
        Unidade unidade = NovaUnidade("slug-reciclado", "RCSL", "REC001");

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(unidade);
            await ctx.SaveChangesAsync();
        }

        // Soft-delete via SoftDeleteInterceptor.
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            Unidade tracked = await ctx.Unidades.SingleAsync(u => u.Id == unidade.Id);
            ctx.Unidades.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        // Confirma que is_deleted = true via IgnoreQueryFilters.
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            Unidade excluida = await ctx.Unidades
                .IgnoreQueryFilters()
                .SingleAsync(u => u.Id == unidade.Id);
            excluida.IsDeleted.Should().BeTrue();
        }

        // Nova Unidade com mesmo slug é aceita — UNIQUE parcial WHERE is_deleted = false.
        Unidade nova = NovaUnidade("slug-reciclado", "RCSL2", "REC002");
        await using OrganizacaoInstitucionalDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.Unidades.Add(nova);

        Func<Task> act = async () => await ctx3.SaveChangesAsync();

        await act.Should().NotThrowAsync("slot liberado pelo soft-delete");
    }

    [Fact(DisplayName = "SoftDeleteInterceptor preenche deleted_by e deleted_at")]
    public async Task SoftDelete_PreencheDeletedByEDeletedAt()
    {
        Unidade unidade = NovaUnidade("soft-del-audit", "SDA", "SDA001");

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(unidade);
            await ctx.SaveChangesAsync();
        }

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            Unidade tracked = await ctx.Unidades.SingleAsync(u => u.Id == unidade.Id);
            ctx.Unidades.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Unidade excluida = await readCtx.Unidades
            .IgnoreQueryFilters()
            .SingleAsync(u => u.Id == unidade.Id);

        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedAt.Should().NotBeNull();
        excluida.DeletedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "FK unidade_superior_id aceita hierarquia válida pai → filho")]
    public async Task Hierarquia_FkAceitaPaiFilhoValido()
    {
        Unidade pai = NovaUnidade("reitoria", "REIT", "REIT001");

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(pai);
            await ctx.SaveChangesAsync();
        }

        Unidade filho = NovaUnidade("proeg", "PROEG", "PROEG001", superiorId: pai.Id);

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(filho);
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext readCtx = _fixture.CreateDbContext(userId: null);

        Unidade persistido = await readCtx.Unidades.SingleAsync(u => u.Id == filho.Id);

        persistido.UnidadeSuperiorId.Should().Be(pai.Id);
    }

    [Fact(DisplayName = "FK unidade_superior_id com RESTRICT bloqueia DELETE físico do pai quando há filhos")]
    public async Task Hierarquia_FkRestrictBloqueiaDeleteFisicoDoSuperior()
    {
        // O SoftDeleteInterceptor converte EntityState.Deleted → UPDATE is_deleted=true,
        // portanto não emite DELETE SQL e não aciona o RESTRICT. Para testar a constraint
        // de banco em si, emitimos DELETE físico via SQL bruto.
        Unidade pai = NovaUnidade("pai-restrict", "PRST", "PRS001");
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(pai);
            await ctx.SaveChangesAsync();
        }

        Unidade filho = NovaUnidade("filho-restrict", "FRST", "FRS001", superiorId: pai.Id);
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(filho);
            await ctx.SaveChangesAsync();
        }

        // Tenta DELETE físico do pai com filho ativo — deve violar FK RESTRICT.
        await using OrganizacaoInstitucionalDbContext rawCtx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () =>
            await rawCtx.Database.ExecuteSqlAsync(
                $"DELETE FROM organizacao.unidade WHERE id = {pai.Id}");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "FK unidade_superior_id com RESTRICT deve impedir DELETE físico de unidade pai que possui filhos referenciando-a");
    }

    [Fact(DisplayName = "EhDescendenteAsync percorre a cadeia de superiores e ignora não-relacionados")]
    public async Task EhDescendenteAsync_PercorreAncestraisContraPostgresReal()
    {
        // Hierarquia A → B → C (A superior de B, B superior de C) + D solta.
        Unidade a = NovaUnidade("hier-a", "HIERA", "HRA001");
        Unidade b = NovaUnidade("hier-b", "HIERB", "HRB001", a.Id);
        Unidade c = NovaUnidade("hier-c", "HIERC", "HRC001", b.Id);
        Unidade d = NovaUnidade("hier-d", "HIERD", "HRD001");

        await using (OrganizacaoInstitucionalDbContext seed = _fixture.CreateDbContext(AdminA))
        {
            // Saves sequenciais respeitam a FK auto-referencial (pai antes do filho).
            seed.Unidades.Add(a);
            await seed.SaveChangesAsync();
            seed.Unidades.Add(b);
            await seed.SaveChangesAsync();
            seed.Unidades.Add(c);
            await seed.SaveChangesAsync();
            seed.Unidades.Add(d);
            await seed.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);

        // C é descendente de A (A→B→C): tornar C superior de A formaria ciclo.
        (await repository.EhDescendenteAsync(c.Id, a.Id, CancellationToken.None)).Should().BeTrue();
        // B é descendente direto de A.
        (await repository.EhDescendenteAsync(b.Id, a.Id, CancellationToken.None)).Should().BeTrue();
        // A não é descendente de C: reapontar A para sob C é válido (sem ciclo).
        (await repository.EhDescendenteAsync(a.Id, c.Id, CancellationToken.None)).Should().BeFalse();
        // Igualdade conta como descendente — barra a auto-referência.
        (await repository.EhDescendenteAsync(a.Id, a.Id, CancellationToken.None)).Should().BeTrue();
        // D não tem vínculo com A.
        (await repository.EhDescendenteAsync(d.Id, a.Id, CancellationToken.None)).Should().BeFalse();
    }

    [Fact(DisplayName = "Checagens de unicidade normalizam (Trim) Sigla e Codigo do argumento")]
    public async Task ChecagensDeUnicidade_NormalizamArgumentoComEspacos()
    {
        Unidade existente = NovaUnidade("trim-check", "TRIMSIG", "TRIMCOD");
        await using (OrganizacaoInstitucionalDbContext seed = _fixture.CreateDbContext(AdminA))
        {
            seed.Unidades.Add(existente);
            await seed.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);

        // " trimsig " deve casar com "TRIMSIG" persistido (Trim + ToUpperInvariant).
        (await repository.SiglaExisteEntreLivosAsync(" trimsig ", null, CancellationToken.None)).Should().BeTrue();
        // " TRIMCOD " deve casar com "TRIMCOD" persistido (Trim).
        (await repository.CodigoExisteEntreLivosAsync(" TRIMCOD ", null, CancellationToken.None)).Should().BeTrue();
    }

    [Fact(DisplayName = "Soft-delete de Unidade preserva o histórico de identificadores (FK não cascateia) — regressão #629")]
    public async Task SoftDelete_PreservaHistoricoDeIdentificadores()
    {
        // Regressão (issue #629 / PR #631): ObterPorIdAsync faz Include(Historico);
        // com a FK em Cascade, Remover(unidade) marcava os históricos carregados como
        // Deleted e — como o SoftDeleteInterceptor só converte ISoftDeletable — eles
        // sofriam hard-delete físico, destruindo a trilha append-only. A FK Restrict
        // impede o cascade em memória: a Unidade vira soft-delete e o histórico sobrevive.
        Unidade unidade = NovaUnidade("hist-preserva", "HPRES", "HPR001");

        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Unidades.Add(unidade);
            await ctx.SaveChangesAsync();
        }

        // Caminho EXATO do RemoverUnidadeCommandHandler: ObterPorIdAsync (Include
        // Historico, rastreado) seguido de Remover, no mesmo contexto.
        await using (OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            var repository = new UnidadeRepository(ctx);
            Unidade tracked = (await repository.ObterPorIdAsync(unidade.Id, CancellationToken.None))!;
            tracked.Historico.Should().HaveCount(3,
                "a criação abriu 3 entradas (Slug, Sigla, Codigo), carregadas e rastreadas");

            repository.Remover(tracked);
            await ctx.SaveChangesAsync();
        }

        await using OrganizacaoInstitucionalDbContext readCtx = _fixture.CreateDbContext(userId: null);

        // Unidade soft-deletada (UPDATE is_deleted=true), não removida fisicamente.
        Unidade excluida = await readCtx.Unidades
            .IgnoreQueryFilters()
            .SingleAsync(u => u.Id == unidade.Id);
        excluida.IsDeleted.Should().BeTrue();

        // Histórico append-only PRESERVADO — as 3 linhas seguem no banco.
        int historicoCount = await readCtx.UnidadesIdentificadoresHistorico
            .CountAsync(h => h.UnidadeId == unidade.Id);
        historicoCount.Should().Be(3,
            "o histórico de identificadores não pode ser hard-deletado no soft-delete da Unidade (issue #629)");
    }

    // ── Factory helper ───────────────────────────────────────────────────

    private static Unidade NovaUnidade(
        string slug,
        string sigla,
        string codigo,
        Guid? superiorId = null,
        string? alias = null) =>
        Unidade.Criar(
            nome: $"Unidade {sigla}",
            alias: alias,
            slug: Slug.From(slug).Value!,
            sigla: sigla,
            codigo: codigo,
            unidadeSuperiorId: superiorId,
            tipo: TipoUnidade.Centro,
            unidadeAcademica: false,
            vigenciaInicio: DataInicio,
            vigenciaFim: null,
            origem: OrigemUnidade.CriadoNoUniPlus).Value!;
}
