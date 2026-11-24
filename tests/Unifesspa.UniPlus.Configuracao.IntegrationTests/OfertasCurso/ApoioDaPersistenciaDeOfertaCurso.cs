namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
using Unifesspa.UniPlus.Testes.Compartilhado;

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
            CodigoUnico(), "Engenharia Elétrica", "Bacharelado", "Graduação", null).ValorEsperado();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Cursos.Add(curso);
        await ctx.SaveChangesAsync();

        return curso.Id;
    }

    internal async Task GravarAsync(OfertaCurso oferta, string usuario = AdminA)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        ctx.OfertasCurso.Add(oferta);
        await ctx.SaveChangesAsync();
    }

    internal async Task RemoverAsync(Guid ofertaId, string usuario = AdminA)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        OfertaCurso rastreada = await ctx.OfertasCurso.SingleAsync(o => o.Id == ofertaId);
        ctx.OfertasCurso.Remove(rastreada);
        await ctx.SaveChangesAsync();
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
            CodigoUnico(), "Engenharia Civil", "Bacharelado", "Graduação", null).ValorEsperado();
        LocalOferta local = LocalOferta.Criar(
            new("CAMPUS_SEDE", null, "1504208", "Marabá", "PA", null, null),
            Agora).ValorEsperado();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Cursos.Add(curso);
        ctx.LocaisOferta.Add(local);
        await ctx.SaveChangesAsync();

        return (curso.Id, local.Id);
    }

    internal static string CodigoCru() => $"CRU_{Guid.CreateVersion7():N}".ToUpperInvariant();

    internal static UnidadeOfertante NovaUnidade() =>
        UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").ValorEsperado();

    internal static OfertaCurso NovaOferta(Guid cursoId, Guid localId, UnidadeOfertante unidade) =>
        OfertaCurso.Criar(
            cursoId, localId, unidade,
            new DeclaracaoDaOfertaDeCurso(
                $"OFERTA_{Guid.CreateVersion7():N}".ToUpperInvariant(), "PARFOR", "SEMIPRESENCIAL", "INTENSIVO", "INTEGRAL", ["NOTURNO", "VESPERTINO"],
                "123456", "ENG-01", 40, "Decreto 6.755/2009", "Portaria MEC 9/2009")).ValorEsperado();

    /// <summary>
    /// A linha gravada por SQL cru, sem passar pelo domínio: serve a provar o que o banco recusa sozinho. As colunas da
    /// unidade ofertante são sempre as mesmas e não entram aqui.
    /// </summary>
    internal sealed record LinhaCruDeOferta(
        string Programa,
        string Formato,
        string Regime,
        string?[] Turnos,
        int? Vagas,
        string? BaseLegal,
        string Funcionamento = "EXTENSIVO");

    internal Task InserirCruAsync(Guid cursoId, Guid localId, LinhaCruDeOferta linha) =>
        ExecutarInsercaoCruaAsync(
            InsercaoDeOfertaCrua,
            [cursoId, localId, linha.Programa, linha.Formato, linha.Funcionamento, linha.Regime, linha.Turnos, linha.Vagas, linha.BaseLegal]);

    // Um array passado como string[] sempre chega unidimensional e com limite
    // inferior 1. As formas malformadas que o CHECK precisa recusar só existem na
    // representação textual do array — daí o parâmetro de texto com cast explícito,
    // que o Postgres interpreta pela função de entrada do tipo.
    internal Task InserirCruComTurnosLiteraisAsync(Guid cursoId, Guid localId, string regime, string turnosLiteral) =>
        ExecutarInsercaoCruaAsync(
            InsercaoDeOfertaCrua,
            [cursoId, localId, "REGULAR", "PRESENCIAL", "EXTENSIVO", regime, turnosLiteral, null, null]);

    /// <summary>A linha sem a coluna do regime de funcionamento, que nasce NOT NULL e sem valor presumido.</summary>
    internal Task InserirCruSemRegimeDeFuncionamentoAsync(Guid cursoId, Guid localId) =>
        ExecutarInsercaoCruaAsync(
            InsercaoSemRegimeDeFuncionamento,
            [cursoId, localId, "REGULAR", "PRESENCIAL", "REGULAR", new[] { "MATUTINO" }, null, null]);

    /// <summary>Insere a linha crua pelo contexto informado, para que o chamador a mantenha na própria transação.</summary>
    internal static Task InserirCruNoContextoAsync(UniPlusDbContext ctx, Guid cursoId, Guid localId, LinhaCruDeOferta linha) =>
        InserirPeloContextoAsync(
            ctx,
            InsercaoDeOfertaCrua,
            [cursoId, localId, linha.Programa, linha.Formato, linha.Funcionamento, linha.Regime, linha.Turnos, linha.Vagas, linha.BaseLegal]);

    private async Task ExecutarInsercaoCruaAsync(string comandoSql, object?[] colunasDaOferta)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await InserirPeloContextoAsync(ctx, comandoSql, colunasDaOferta);
    }

    private static async Task InserirPeloContextoAsync(UniPlusDbContext ctx, string comandoSql, object?[] colunasDaOferta)
    {
        object?[] valores = [Guid.CreateVersion7(), CodigoCru(), .. colunasDaOferta[..2], Guid.CreateVersion7(), .. colunasDaOferta[2..], DateTimeOffset.UtcNow];

        await ctx.Database.ExecuteSqlAsync(FormattableStringFactory.Create(comandoSql, valores));
    }

    private const string InsercaoDeOfertaCrua = """
        INSERT INTO configuracao.ofertas_de_curso
            (id, codigo, curso_id, local_oferta_id,
             unidade_ofertante_origem_id, unidade_ofertante_sigla, unidade_ofertante_nome, unidade_ofertante_tipo,
             programa_de_oferta, formato_pedagogico, regime_de_funcionamento,
             regime_de_turno, turnos,
             vagas_anuais_autorizadas, base_legal, created_at, is_deleted)
        VALUES
            ({0}, {1}, {2}, {3},
             {4}, 'FACET', 'Faculdade de Computação', 'Faculdade',
             {5}, {6}, {7},
             {8}, {9}::varchar(30)[],
             {10}, {11}, {12}, false)
        """;

    private const string InsercaoSemRegimeDeFuncionamento = """
        INSERT INTO configuracao.ofertas_de_curso
            (id, codigo, curso_id, local_oferta_id,
             unidade_ofertante_origem_id, unidade_ofertante_sigla, unidade_ofertante_nome, unidade_ofertante_tipo,
             programa_de_oferta, formato_pedagogico, regime_de_turno, turnos,
             vagas_anuais_autorizadas, base_legal, created_at, is_deleted)
        VALUES
            ({0}, {1}, {2}, {3},
             {4}, 'FACET', 'Faculdade de Computação', 'Faculdade',
             {5}, {6}, {7}, {8}::varchar(30)[],
             {9}, {10}, {11}, false)
        """;

    internal static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
