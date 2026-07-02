namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;

/// <summary>
/// Integração ponta-a-ponta da OfertaCurso contra Postgres real (story #588,
/// issue #749): persistência com owned type <c>unidade_oft_*</c> (roundtrip do
/// snapshot ADR-0061), FKs intra-schema reais (curso/local_oferta, RESTRICT),
/// CHECKs de domínio via SQL cru (programa/formato/turno/vagas/base legal
/// condicional), soft-delete e o EXISTS real de
/// <c>CursoRepository.ReferenciadoPorOfertaCursoVivaAsync</c>.
/// </summary>
/// <remarks>
/// O snapshot da unidade ofertante já chega congelado no VO — a persistência não
/// precisa de Unidade real (o schema <c>organizacao</c> nem existe nesta fixture);
/// o congelamento com Unidade real é coberto nos endpoint tests.
/// </remarks>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class OfertaCursoPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly DateTimeOffset Agora = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoDbFixture _fixture;

    public OfertaCursoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos, o snapshot unidade_oft_* faz roundtrip e a auditoria é carimbada")]
    public async Task Insert_Completa_PersisteComOwnedRoundtrip()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();
        UnidadeOfertante unidade = NovaUnidade();
        OfertaCurso oferta = NovaOferta(cursoId, localId, unidade);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.OfertasCurso.Add(oferta);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        OfertaCurso persistida = await readCtx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);

        persistida.CursoId.Should().Be(cursoId);
        persistida.LocalOfertaId.Should().Be(localId);
        persistida.UnidadeOfertante.Should().Be(unidade, "o owned type reidrata o snapshot por igualdade estrutural");
        persistida.ProgramaDeOferta.Should().Be(ProgramaDeOferta.Parfor);
        persistida.FormatoPedagogico.Should().Be(FormatoPedagogico.Semipresencial);
        persistida.Turno.Should().Be(TurnoOferta.Noturno);
        persistida.EMecCodigo.Should().Be("123456");
        persistida.CodigoSga.Should().Be("ENG-01");
        persistida.VagasAnuaisAutorizadas.Should().Be(40);
        persistida.BaseLegal.Should().Be("Decreto 6.755/2009");
        persistida.AtoAutorizacaoMec.Should().Be("Portaria MEC 9/2009");
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Turno e campos opcionais nulos persistem nulos e reidratam como nulos")]
    public async Task Insert_OpcionaisNulos_PersisteNulos()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();
        OfertaCurso oferta = OfertaCurso.Criar(
            cursoId, localId, NovaUnidade(), "REGULAR", null,
            null, null, null, null, null, null).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.OfertasCurso.Add(oferta);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        OfertaCurso persistida = await readCtx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);

        persistida.FormatoPedagogico.Should().Be(FormatoPedagogico.Presencial, "default conceitual quando o token está ausente");
        persistida.Turno.Should().BeNull();
        persistida.EMecCodigo.Should().BeNull();
        persistida.CodigoSga.Should().BeNull();
        persistida.VagasAnuaisAutorizadas.Should().BeNull();
        persistida.BaseLegal.Should().BeNull();
        persistida.AtoAutorizacaoMec.Should().BeNull();
    }

    [Fact(DisplayName = "FK real: oferta apontando curso inexistente é rejeitada pelo banco (23503)")]
    public async Task Fk_CursoInexistente_Rejeita()
    {
        (_, Guid localId) = await SemearCursoELocalAsync();
        OfertaCurso orfa = NovaOferta(Guid.CreateVersion7(), localId, NovaUnidade());

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.OfertasCurso.Add(orfa);

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23503", "curso_id tem FK intra-schema real para configuracao.curso");
    }

    [Fact(DisplayName = "FK real: oferta apontando local de oferta inexistente é rejeitada pelo banco (23503)")]
    public async Task Fk_LocalOfertaInexistente_Rejeita()
    {
        (Guid cursoId, _) = await SemearCursoELocalAsync();
        OfertaCurso orfa = NovaOferta(cursoId, Guid.CreateVersion7(), NovaUnidade());

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.OfertasCurso.Add(orfa);

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23503", "local_oferta_id tem FK intra-schema real para configuracao.local_oferta");
    }

    [Fact(DisplayName = "CHECK de banco rejeita programa de oferta fora do domínio via SQL cru")]
    public async Task Check_RejeitaProgramaForaDoDominioViaSqlCru()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();

        Func<Task> act = () => InserirCruAsync(
            cursoId, localId, programa: "PROUNI", formato: "PRESENCIAL", turno: null, vagas: null, baseLegal: "Lei X");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_oferta_curso_programa_de_oferta impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita turno fora do domínio via SQL cru, mas aceita turno nulo (null-safe)")]
    public async Task Check_Turno_NullSafe()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();

        Func<Task> invalido = () => InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL", turno: "DIURNO", vagas: null, baseLegal: null);
        await invalido.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_oferta_curso_turno impede token fora do domínio");

        Func<Task> nulo = () => InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL", turno: null, vagas: null, baseLegal: null);
        await nulo.Should().NotThrowAsync("turno é opcional e o CHECK é null-safe");
    }

    [Fact(DisplayName = "CHECK de banco rejeita vagas negativas via SQL cru, mas aceita zero e nulo")]
    public async Task Check_VagasAnuais_RejeitaNegativas()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();

        Func<Task> negativa = () => InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL", turno: null, vagas: -1, baseLegal: null);
        await negativa.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_oferta_curso_vagas_anuais_autorizadas impede teto negativo");

        Func<Task> zero = () => InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL", turno: null, vagas: 0, baseLegal: null);
        await zero.Should().NotThrowAsync("zero é teto válido no e-MEC");
    }

    [Fact(DisplayName = "CHECK condicional da base legal via SQL cru: PARFOR sem base é rejeitado; REGULAR sem base passa")]
    public async Task Check_BaseLegalCondicional()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();

        Func<Task> parforSemBase = () => InserirCruAsync(
            cursoId, localId, programa: "PARFOR", formato: "PRESENCIAL", turno: null, vagas: null, baseLegal: null);
        await parforSemBase.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_oferta_curso_base_legal_programa espelha o guard de domínio (ADR-0066)");

        Func<Task> regularSemBase = () => InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL", turno: null, vagas: null, baseLegal: null);
        await regularSemBase.Should().NotThrowAsync("REGULAR não exige base legal");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e tira a oferta das leituras via query filter")]
    public async Task SoftDelete_PreservaTrilha()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();
        OfertaCurso oferta = NovaOferta(cursoId, localId, NovaUnidade());

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.OfertasCurso.Add(oferta);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            OfertaCurso tracked = await ctx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);
            ctx.OfertasCurso.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        (await readCtx.OfertasCurso.AnyAsync(o => o.Id == oferta.Id))
            .Should().BeFalse("o soft-delete tira a oferta das leituras");

        OfertaCurso excluida = await readCtx.OfertasCurso
            .IgnoreQueryFilters().SingleAsync(o => o.Id == oferta.Id);
        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "ReferenciadoPorOfertaCursoVivaAsync: true com oferta viva; false após soft-delete da oferta")]
    public async Task ReferenciadoPorOfertaCursoViva_ReflecteVida()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();
        OfertaCurso oferta = NovaOferta(cursoId, localId, NovaUnidade());

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.OfertasCurso.Add(oferta);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            var cursoRepository = new CursoRepository(ctx);
            (await cursoRepository.ReferenciadoPorOfertaCursoVivaAsync(cursoId, CancellationToken.None))
                .Should().BeTrue("há oferta viva referenciando o curso — a remoção do curso deve bloquear (CA-05)");
            (await cursoRepository.ReferenciadoPorOfertaCursoVivaAsync(Guid.CreateVersion7(), CancellationToken.None))
                .Should().BeFalse("curso sem oferta não é referenciado");
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            OfertaCurso tracked = await ctx.OfertasCurso.SingleAsync(o => o.Id == oferta.Id);
            ctx.OfertasCurso.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var repositoryAposRemocao = new CursoRepository(readCtx);
        (await repositoryAposRemocao.ReferenciadoPorOfertaCursoVivaAsync(cursoId, CancellationToken.None))
            .Should().BeFalse("o soft-delete da oferta libera o curso para remoção");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<(Guid CursoId, Guid LocalOfertaId)> SemearCursoELocalAsync()
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), "Engenharia Civil", "Bacharelado", "Graduação", null).Value!;
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Cursos.Add(curso);
        ctx.LocaisOferta.Add(local);
        await ctx.SaveChangesAsync();

        return (curso.Id, local.Id);
    }

    private static UnidadeOfertante NovaUnidade() =>
        UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;

    private static OfertaCurso NovaOferta(Guid cursoId, Guid localId, UnidadeOfertante unidade) =>
        OfertaCurso.Criar(
            cursoId, localId, unidade, "PARFOR", "SEMIPRESENCIAL", "NOTURNO",
            "123456", "ENG-01", 40, "Decreto 6.755/2009", "Portaria MEC 9/2009").Value!;

    private async Task InserirCruAsync(
        Guid cursoId,
        Guid localId,
        string programa,
        string formato,
        string? turno,
        int? vagas,
        string? baseLegal)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.oferta_curso
                (id, curso_id, local_oferta_id,
                 unidade_oft_origem_id, unidade_oft_sigla, unidade_oft_nome, unidade_oft_tipo,
                 programa_de_oferta, formato_pedagogico, turno, vagas_anuais_autorizadas, base_legal,
                 created_at, is_deleted)
            VALUES
                ({Guid.CreateVersion7()}, {cursoId}, {localId},
                 {Guid.CreateVersion7()}, {"FACET"}, {"Faculdade de Computação"}, {"Faculdade"},
                 {programa}, {formato}, {turno}, {vagas}, {baseLegal},
                 {DateTimeOffset.UtcNow}, {false})
            """);
    }

    private static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
