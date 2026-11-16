namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDaPersistenciaDeOfertaCurso;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class OfertaCursoRestricoesDoBancoTests
{
    private readonly ConfiguracaoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDeOfertaCurso _apoio;

    public OfertaCursoRestricoesDoBancoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "FK real: oferta apontando curso inexistente é rejeitada pelo banco (23503)")]
    public async Task Fk_CursoInexistente_Rejeita()
    {
        (_, Guid localId) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso orfa = NovaOferta(Guid.CreateVersion7(), localId, NovaUnidade());

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.OfertasCurso.Add(orfa);

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23503", "curso_id tem FK intra-schema real para configuracao.cursos");
    }

    [Fact(DisplayName = "FK real: oferta apontando local de oferta inexistente é rejeitada pelo banco (23503)")]
    public async Task Fk_LocalOfertaInexistente_Rejeita()
    {
        (Guid cursoId, _) = await _apoio.SemearCursoELocalAsync();
        OfertaCurso orfa = NovaOferta(cursoId, Guid.CreateVersion7(), NovaUnidade());

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.OfertasCurso.Add(orfa);

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23503", "local_oferta_id tem FK intra-schema real para configuracao.locais_de_oferta");
    }

    [Fact(DisplayName = "CHECK de banco rejeita programa de oferta fora do domínio via SQL cru")]
    public async Task Check_RejeitaProgramaForaDoDominioViaSqlCru()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "PROUNI", formato: "PRESENCIAL", regime: "REGULAR", turnos: ["MATUTINO"], vagas: null, baseLegal: "Lei X");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_ofertas_de_curso_programa_de_oferta impede o INSERT direto");
    }

    [Theory(DisplayName = "CHECK de banco rejeita token de turno fora do domínio via SQL cru")]
    [InlineData("DIURNO")]
    [InlineData("INTEGRAL")]
    [InlineData("Matutino")]
    public async Task Check_TurnosDominio_RejeitaTokenForaDoDominio(string token)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: "REGULAR", turnos: [token], vagas: null, baseLegal: null);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_ofertas_de_curso_turnos_dominio impede token fora do domínio");
    }

    [Fact(DisplayName = "CHECK de banco rejeita elemento nulo dentro do array de turnos")]
    public async Task Check_TurnosDominio_RejeitaElementoNulo()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: "INTEGRAL", turnos: ["MATUTINO", null!], vagas: null, baseLegal: null);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "containment não considera NULL contido — o elemento nulo cai no CHECK de domínio");
    }

    [Fact(DisplayName = "CHECK de banco rejeita regime fora do domínio via SQL cru")]
    public async Task Check_RegimeDeTurno_RejeitaForaDoDominio()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: "PARCIAL", turnos: ["MATUTINO"], vagas: null, baseLegal: null);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_ofertas_de_curso_regime_de_turno impede token fora do domínio");
    }

    [Theory(DisplayName = "CHECK de banco espelha a cardinalidade que cada regime exige")]
    [InlineData("REGULAR", new string[0])]
    [InlineData("REGULAR", new[] { "MATUTINO", "NOTURNO" })]
    [InlineData("INTEGRAL", new string[0])]
    [InlineData("INTEGRAL", new[] { "NOTURNO" })]
    [InlineData("INTEGRAL", new[] { "MATUTINO", "VESPERTINO", "NOTURNO" })]
    [InlineData("INTEGRAL", new[] { "MATUTINO", "MATUTINO" })]
    public async Task Check_TurnosRegime_RejeitaCardinalidadeIncompativel(string regime, string[] turnos)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: regime, turnos: turnos, vagas: null, baseLegal: null);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_ofertas_de_curso_turnos_regime espelha a invariante do agregado");
    }

    [Theory(DisplayName = "CHECK de banco rejeita regime de funcionamento fora do domínio via SQL cru")]
    [InlineData("SEMI_INTENSIVO")]
    [InlineData("Intensivo")]
    [InlineData("INTEGRAL")]
    [InlineData("")]
    public async Task Check_RegimeDeFuncionamento_RejeitaForaDoDominio(string token)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: "REGULAR", turnos: ["MATUTINO"], vagas: null, baseLegal: null,
            funcionamento: token);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_ofertas_de_curso_regime_de_funcionamento impede token fora do domínio");
    }

    [Fact(DisplayName = "CHECK de banco recusa a oferta intensiva com regime de turno regular")]
    public async Task Check_FuncionamentoRegimeDeTurno_RecusaIntensivoRegular()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: "REGULAR", turnos: ["MATUTINO"], vagas: null, baseLegal: null,
            funcionamento: "INTENSIVO");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_ofertas_de_curso_funcionamento_regime_de_turno espelha a invariante do agregado");
    }

    [Theory(DisplayName = "CHECK de banco aceita as cinco combinações válidas de funcionamento e regime de turno")]
    [InlineData("EXTENSIVO", "REGULAR", new[] { "MATUTINO" })]
    [InlineData("EXTENSIVO", "INTEGRAL", new[] { "MATUTINO", "VESPERTINO" })]
    [InlineData("ALTERNANCIA_PEDAGOGICA", "REGULAR", new[] { "MATUTINO" })]
    [InlineData("ALTERNANCIA_PEDAGOGICA", "INTEGRAL", new[] { "MATUTINO", "VESPERTINO" })]
    [InlineData("INTENSIVO", "INTEGRAL", new[] { "MATUTINO", "VESPERTINO" })]
    public async Task Check_FuncionamentoRegimeDeTurno_AceitaCombinacoesValidas(
        string funcionamento, string regime, string[] turnos)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: regime, turnos: turnos, vagas: null, baseLegal: null,
            funcionamento: funcionamento);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Coluna do regime de funcionamento é NOT NULL — nenhum default preenche a ausência")]
    public async Task Insert_SemRegimeDeFuncionamento_ViolaNotNull()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        Func<Task> act = () => ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.ofertas_de_curso
                (id, codigo, curso_id, local_oferta_id,
                 unidade_oft_origem_id, unidade_oft_sigla, unidade_oft_nome, unidade_oft_tipo,
                 programa_de_oferta, formato_pedagogico, regime_de_turno, turnos,
                 vagas_anuais_autorizadas, base_legal, created_at, is_deleted)
            VALUES
                ({Guid.CreateVersion7()}, {CodigoCru()}, {cursoId}, {localId},
                 {Guid.CreateVersion7()}, {"FACET"}, {"Faculdade de Computação"}, {"Faculdade"},
                 {"REGULAR"}, {"PRESENCIAL"}, {"REGULAR"}, {new[] { "MATUTINO" }}::varchar(30)[],
                 NULL, NULL, {DateTimeOffset.UtcNow}, {false})
            """);

        Npgsql.PostgresException pg =
            (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be(
            "23502",
            "a coluna nasce NOT NULL sem defaultValue — omitir o regime não vira valor presumido");
    }

    [Theory(DisplayName = "CHECK de banco aceita as combinações válidas de regime e turnos")]
    [InlineData("REGULAR", new[] { "MATUTINO" })]
    [InlineData("REGULAR", new[] { "NOTURNO" })]
    [InlineData("INTEGRAL", new[] { "MATUTINO", "VESPERTINO" })]
    [InlineData("INTEGRAL", new[] { "VESPERTINO", "NOTURNO" })]
    public async Task Check_TurnosRegime_AceitaCombinacoesValidas(string regime, string[] turnos)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: regime, turnos: turnos, vagas: null, baseLegal: null);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Linha gravada com os turnos fora de ordem é lida em ordem canônica")]
    public async Task Leitura_TurnosGravadosForaDeOrdem_DevolveOrdemCanonica()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        await _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: "INTEGRAL", turnos: ["NOTURNO", "MATUTINO"], vagas: null, baseLegal: null);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new OfertaCursoReader(readCtx);

        IReadOnlyList<OfertaCursoView> vivas = await reader.ListarVivasAsync();
        OfertaCursoView view = vivas.Single(v => v.CursoId == cursoId);

        view.Turnos.Should().Equal(["MATUTINO", "NOTURNO"],
            "a ordem canônica é promessa do contrato de leitura, não consequência da ordem gravada");
    }

    [Theory(DisplayName = "CHECK de banco rejeita array de turnos malformado — multidimensional ou com limite inferior fora de 1")]
    [InlineData("[0:1]={MATUTINO,MATUTINO}", "limite inferior 0 faz turnos[2] devolver NULL")]
    [InlineData("[2:3]={MATUTINO,VESPERTINO}", "limite inferior 2 faz turnos[1] devolver NULL")]
    [InlineData("{{MATUTINO,VESPERTINO}}", "array 2-D tem cardinalidade 2 mas o subscrito simples devolve NULL")]
    public async Task Check_TurnosRegime_RejeitaArrayMalformado(string literalDoArray, string porque)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> act = () => _apoio.InserirCruComTurnosLiteraisAsync(
            cursoId, localId, regime: "INTEGRAL", turnosLiteral: literalDoArray);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(porque);
    }

    [Fact(DisplayName = "CHECK de banco rejeita vagas negativas via SQL cru, mas aceita zero e nulo")]
    public async Task Check_VagasAnuais_RejeitaNegativas()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> negativa = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL", regime: "REGULAR", turnos: ["MATUTINO"], vagas: -1, baseLegal: null);
        await negativa.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_ofertas_de_curso_vagas_anuais_autorizadas impede teto negativo");

        Func<Task> zero = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL", regime: "REGULAR", turnos: ["MATUTINO"], vagas: 0, baseLegal: null);
        await zero.Should().NotThrowAsync("zero é teto válido no e-MEC");
    }

    [Fact(DisplayName = "CHECK condicional da base legal via SQL cru: PARFOR sem base é rejeitado; REGULAR sem base passa")]
    public async Task Check_BaseLegalCondicional()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();

        Func<Task> parforSemBase = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "PARFOR", formato: "PRESENCIAL",
            regime: "REGULAR", turnos: ["MATUTINO"], vagas: null, baseLegal: null);
        await parforSemBase.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_ofertas_de_curso_base_legal_programa espelha o guard de domínio (ADR-0066)");

        Func<Task> regularSemBase = () => _apoio.InserirCruAsync(
            cursoId, localId, programa: "REGULAR", formato: "PRESENCIAL",
            regime: "REGULAR", turnos: ["MATUTINO"], vagas: null, baseLegal: null);
        await regularSemBase.Should().NotThrowAsync("REGULAR não exige base legal");
    }
}
