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

internal sealed class ApoioDaPersistenciaDeOfertaCurso
{
    private readonly ConfiguracaoDbFixture _fixture;

    internal ApoioDaPersistenciaDeOfertaCurso(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    internal const string AdminA = "admin-a";

    internal const string AdminB = "admin-b";

    internal static readonly DateTimeOffset Agora = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A ordem que a listagem usa quando a consulta não pede outra.</summary>
    internal static readonly IReadOnlyList<SortField> OrdenacaoPadraoDeOferta =

    [
        new(CamposOrdenacaoOfertaCurso.CursoNome, SortDirection.Ascending),
        new(CamposOrdenacaoOfertaCurso.CursoCodigo, SortDirection.Ascending),
    ];

    internal async Task<Guid> SemearCursoAsync()
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), "Engenharia Elétrica", "Bacharelado", "Graduação", null).Value!;

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Cursos.Add(curso);
        await ctx.SaveChangesAsync();

        return curso.Id;
    }

    internal async Task SemearOfertasAsync(params (Guid CursoId, Guid LocalOfertaId)[] ofertas)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        foreach ((Guid cursoId, Guid localOfertaId) in ofertas)
        {
            ctx.OfertasCurso.Add(NovaOferta(cursoId, localOfertaId, NovaUnidade()));
        }

        await ctx.SaveChangesAsync();
    }

    internal async Task<(Guid CursoId, Guid LocalOfertaId)> SemearCursoELocalAsync()
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), "Engenharia Civil", "Bacharelado", "Graduação", null).Value!;
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Cursos.Add(curso);
        ctx.LocaisOferta.Add(local);
        await ctx.SaveChangesAsync();

        return (curso.Id, local.Id);
    }

    internal static string CodigoCru() => $"CRU_{Guid.CreateVersion7():N}".ToUpperInvariant();

    internal static UnidadeOfertante NovaUnidade() =>
        UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;

    internal static OfertaCurso NovaOferta(Guid cursoId, Guid localId, UnidadeOfertante unidade) =>
        OfertaCurso.Criar(
            cursoId, localId, unidade,
            new DeclaracaoDaOfertaDeCurso(
                $"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant(), "PARFOR", "SEMIPRESENCIAL", "INTENSIVO", "INTEGRAL", ["NOTURNO", "VESPERTINO"],
                "123456", "ENG-01", 40, "Decreto 6.755/2009", "Portaria MEC 9/2009")).Value!;

    internal async Task InserirCruAsync(
        Guid cursoId,
        Guid localId,
        string programa,
        string formato,
        string regime,
        string?[] turnos,
        int? vagas,
        string? baseLegal,
        string funcionamento = "EXTENSIVO")
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.ofertas_de_curso
                (id, codigo, curso_id, local_oferta_id,
                 unidade_oft_origem_id, unidade_oft_sigla, unidade_oft_nome, unidade_oft_tipo,
                 programa_de_oferta, formato_pedagogico, regime_de_funcionamento,
                 regime_de_turno, turnos,
                 vagas_anuais_autorizadas, base_legal, created_at, is_deleted)
            VALUES
                ({Guid.CreateVersion7()}, {CodigoCru()}, {cursoId}, {localId},
                 {Guid.CreateVersion7()}, {"FACET"}, {"Faculdade de Computação"}, {"Faculdade"},
                 {programa}, {formato}, {funcionamento},
                 {regime}, {turnos}::varchar(30)[],
                 {vagas}, {baseLegal}, {DateTimeOffset.UtcNow}, {false})
            """);
    }

    // Um array passado como string[] sempre chega unidimensional e com limite
    // inferior 1. As formas malformadas que o CHECK precisa recusar só existem na
    // representação textual do array — daí o parâmetro de texto com cast explícito,
    // que o Postgres interpreta pela função de entrada do tipo.
    internal async Task InserirCruComTurnosLiteraisAsync(
        Guid cursoId,
        Guid localId,
        string regime,
        string turnosLiteral)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.ofertas_de_curso
                (id, codigo, curso_id, local_oferta_id,
                 unidade_oft_origem_id, unidade_oft_sigla, unidade_oft_nome, unidade_oft_tipo,
                 programa_de_oferta, formato_pedagogico, regime_de_funcionamento,
                 regime_de_turno, turnos,
                 vagas_anuais_autorizadas, base_legal, created_at, is_deleted)
            VALUES
                ({Guid.CreateVersion7()}, {CodigoCru()}, {cursoId}, {localId},
                 {Guid.CreateVersion7()}, {"FACET"}, {"Faculdade de Computação"}, {"Faculdade"},
                 {"REGULAR"}, {"PRESENCIAL"}, {"EXTENSIVO"},
                 {regime}, {turnosLiteral}::varchar(30)[],
                 NULL, NULL, {DateTimeOffset.UtcNow}, {false})
            """);
    }

    internal static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
