namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposConceito;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O catálogo de tipos de conceito contra Postgres real: os valores do categórico gravam e voltam na ordem
/// informada, são substituídos em bloco, e o código é único entre todos os tipos, inclusive os desativados.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class TipoConceitoPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public TipoConceitoPersistenceTests(ConfiguracaoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O tipo categórico grava os valores e os relê na ordem informada, e não na do código")]
    public async Task Categorico_GravaEReleOsValoresEmOrdem()
    {
        TipoConceito tipo = Novo(Categorico("REPROVADO", "APROVADO", "ABSTENCAO"));
        await Gravar(tipo);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        TipoConceito relido = await leitura.Set<TipoConceito>().SingleAsync(t => t.Id == tipo.Id);

        relido.Valores.Select(valor => valor.Codigo).Should().Equal("REPROVADO", "APROVADO", "ABSTENCAO");
        relido.Valores.Select(valor => valor.Posicao).Should().Equal(1, 2, 3);
        (await CodigosPorPosicaoAsync(leitura, tipo.Id)).Should().Equal("REPROVADO", "APROVADO", "ABSTENCAO");
        relido.Valores.Single(valor => valor.Codigo == "APROVADO").ContaComoAprovacao.Should().BeTrue();
    }

    [Fact(DisplayName = "Editar o tipo categórico substitui os valores em bloco, sem deixar os antigos")]
    public async Task Categorico_EditarSubstituiOsValoresEmBloco()
    {
        TipoConceito tipo = Novo(Categorico("REPROVADO", "APROVADO", "ABSTENCAO"));
        await Gravar(tipo);

        await using (UniPlusDbContext edicao = _fixture.CreateDbContext(userId: null))
        {
            TipoConceito gravado = await edicao.Set<TipoConceito>().SingleAsync(t => t.Id == tipo.Id);
            gravado.Atualizar(Categorico("PLENO", "PARCIAL").Validar().Value!);
            await edicao.SaveChangesAsync();
        }

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        (await CodigosPorPosicaoAsync(leitura, tipo.Id)).Should().Equal("PLENO", "PARCIAL");
    }

    [Fact(DisplayName = "O tipo numérico grava a escala e não grava valor categórico")]
    public async Task Numerico_GravaAEscalaSemValores()
    {
        TipoConceito tipo = Novo(Numerico());
        await Gravar(tipo);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        TipoConceito relido = await leitura.Set<TipoConceito>().SingleAsync(t => t.Id == tipo.Id);

        (relido.EscalaMinima, relido.EscalaMaxima, relido.CasasDecimais).Should().Be((0m, 10m, 2));
        relido.Valores.Should().BeEmpty();
    }

    [Fact(DisplayName = "O índice único recusa um segundo tipo com o mesmo código")]
    public async Task Codigo_Repetido_ViolaOIndiceUnico()
    {
        TipoConceito primeiro = Novo(Numerico());
        await Gravar(primeiro);

        PostgresException erro = await GravarEsperandoRecusa(Novo(Categorico("A", "B"), primeiro.Codigo));

        (erro.SqlState, erro.ConstraintName).Should().Be(("23505", "ix_tipos_conceito_codigo"));
    }

    [Fact(DisplayName = "O código de um tipo desativado continua reservado")]
    public async Task Codigo_DeTipoDesativado_ContinuaReservado()
    {
        TipoConceito primeiro = Novo(Numerico());
        primeiro.Desativar();
        await Gravar(primeiro);

        PostgresException erro = await GravarEsperandoRecusa(Novo(Numerico(), primeiro.Codigo));

        erro.ConstraintName.Should().Be("ix_tipos_conceito_codigo");
    }

    [Fact(DisplayName = "O banco recusa escala numérica com o mínimo no máximo ou acima dele")]
    public async Task Check_EscalaInvertida_Recusa()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.tipos_conceito (id, codigo, nome, ativo, natureza, escala_minima, escala_maxima, casas_decimais, created_at)
            VALUES ({Guid.CreateVersion7()}, {Codigo()}, {"Escala invertida"}, {true}, {"NUMERICO"}, {10m}, {0m}, {2}, {DateTimeOffset.UtcNow})
            """);

        PostgresException erro = (await act.Should().ThrowAsync<PostgresException>()).Which;
        (erro.SqlState, erro.ConstraintName).Should().Be(("23514", "ck_tipos_conceito_parametros_da_natureza"));
    }

    private static string Codigo() => TiposConceitoHttpDeTeste.CodigoUnico();

    private static DefinicaoInformada Numerico() =>
        new("Nota da entrevista", null, "numerico", new EscalaInformada(0m, 10m, 2), null);

    private static DefinicaoInformada Categorico(params string[] codigos) =>
        new("Parecer da análise", null, "categorico", null,
            [.. codigos.Select(codigo => new ValorInformado(codigo, $"Rótulo {codigo}", codigo == "APROVADO"))]);

    private static TipoConceito Novo(DefinicaoInformada definicao, string? codigo = null) =>
        TipoConceito.Criar(codigo ?? Codigo(), definicao).Value!;

    private async Task Gravar(TipoConceito tipo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.Set<TipoConceito>().Add(tipo);
        await ctx.SaveChangesAsync();
    }

    private async Task<PostgresException> GravarEsperandoRecusa(TipoConceito tipo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.Set<TipoConceito>().Add(tipo);

        Func<Task> gravar = async () => await ctx.SaveChangesAsync();
        DbUpdateException erro = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;
        return erro.InnerException.Should().BeOfType<PostgresException>().Which;
    }

    private static async Task<List<string>> CodigosPorPosicaoAsync(UniPlusDbContext ctx, Guid tipoId) =>
        await ctx.Database.SqlQuery<string>(
            $"""SELECT codigo AS "Value" FROM configuracao.valores_tipo_conceito WHERE tipo_conceito_id = {tipoId} ORDER BY posicao""")
            .ToListAsync();
}
