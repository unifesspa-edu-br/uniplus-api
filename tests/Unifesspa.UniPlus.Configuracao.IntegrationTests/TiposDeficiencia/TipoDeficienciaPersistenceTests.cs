namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia.DadosDeTipoDeficiencia;

/// <summary>
/// Integração ponta-a-ponta do TipoDeficiencia contra Postgres real (UNI-REQ-0012,
/// UNI-REQ-0061): persistência, UNIQUE parcial do código e do nome vivos,
/// liberação dos slots por soft-delete e leitura cross-módulo ordenada por nome.
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
        string codigo = CodigoUnico();
        string nome = NomeDePersistenciaUnico();
        TipoDeficiencia tipo = Novo(codigo, nome);

        await PersistirAsync(tipo);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoDeficiencia persistido = await readCtx.TiposDeficiencia.SingleAsync(t => t.Id == tipo.Id);

        VerificarTipoPersistido(persistido, codigo, nome);

        await VerificarLeituraPorIdAsync(readCtx, tipo.Id, codigo, nome);
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segundo tipo vivo com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await PersistirAsync(Novo(codigo, NomeDePersistenciaUnico()));

        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.TiposDeficiencia.Add(Novo(codigo, NomeDePersistenciaUnico()));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsCodigoConflict) em
        // CodigoJaExiste/409: SqlState 23505 + nome do índice único parcial.
        await VerificarViolacaoDeUnicidadeAsync(act, RestricoesDoBanco.TipoDeDeficienciaCodigoAtivos);
    }

    [Fact(DisplayName = "UNIQUE parcial do nome rejeita segundo tipo vivo com mesmo nome")]
    public async Task UniquePartial_Nome_RejeitaDuplicataAtiva()
    {
        string nome = NomeDePersistenciaUnico();
        await PersistirAsync(Novo(CodigoUnico(), nome));

        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.TiposDeficiencia.Add(Novo(CodigoUnico(), nome));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Mesma trava, agora para IsNomeConflict → NomeJaExiste/409: como a tabela
        // tem dois índices únicos parciais, o handler precisa distinguir qual foi
        // violado — devolver CodigoJaExiste aqui mentiria sobre a causa.
        await VerificarViolacaoDeUnicidadeAsync(act, RestricoesDoBanco.TipoDeDeficienciaNomeAtivos);
    }

    [Fact(DisplayName = "Código e nome distintos são aceitos")]
    public async Task CodigoENomeDistintos_Aceita()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.TiposDeficiencia.Add(Novo(CodigoUnico(), NomeDePersistenciaUnico()));
        ctx.TiposDeficiencia.Add(Novo(CodigoUnico(), NomeDePersistenciaUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("código e nome são distintos");
    }

    [Fact(DisplayName = "CHECK de formato do código rejeita insert cru fora do padrão canônico")]
    public async Task CheckConstraint_Codigo_RejeitaFormatoInvalido()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);

        // Bypassa o value object de propósito: o CHECK é defesa em profundidade
        // contra escrita fora do fluxo da aplicação.
        Func<Task> act = () => InserirCodigoInvalidoAsync(ctx);

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be("23514", "violação de CHECK constraint");
        pg.ConstraintName.Should().Be("ck_tipos_de_deficiencia_codigo_formato");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera os slots das UNIQUE parciais")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlots()
    {
        string codigo = CodigoUnico();
        string nome = NomeDePersistenciaUnico();
        TipoDeficiencia tipo = Novo(codigo, nome);
        await PersistirAsync(tipo);

        await RemoverAsync(tipo.Id);
        await VerificarTrilhaDeExclusaoAsync(tipo.Id, codigo);

        await using UniPlusDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.TiposDeficiencia.Add(Novo(codigo, nome));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("os slots de código e nome foram liberados pelo soft-delete");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por nome, expõe o código e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorNomeExpoeCodigoEExcluiSoftDeleted()
    {
        string prefixo = $"DEF_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        string nomeA = $"{prefixo}_A";
        string nomeB = $"{prefixo}_B";
        string nomeExcluido = $"{prefixo}_D";
        string codigoA = CodigoUnico();

        await PrepararListagemAsync(nomeA, nomeB, nomeExcluido, codigoA);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoDeficienciaReader(readCtx);
        IReadOnlyList<TipoDeficienciaView> todos = await reader.ListarVivosAsync();

        VerificarTiposListados(todos, prefixo, nomeA, nomeB, codigoA);
    }

    private static void VerificarTipoPersistido(TipoDeficiencia tipo, string codigo, string nome)
    {
        tipo.Codigo.Valor.Should().Be(codigo);
        tipo.Nome.Should().Be(nome);
        tipo.CreatedBy.Should().Be(AdminA);
        tipo.IsDeleted.Should().BeFalse();
    }

    private static void VerificarTiposListados(
        IReadOnlyList<TipoDeficienciaView> todos, string prefixo, string nomeA, string nomeB, string codigoA)
    {
        TipoDeficienciaView[] meus = [.. todos
            .Where(v => v.Nome.StartsWith(prefixo, StringComparison.Ordinal))];
        meus.Select(v => v.Nome).Should().Equal([nomeA, nomeB]);
        meus[0].Codigo.Should().Be(codigoA);
    }

    private static async Task InserirCodigoInvalidoAsync(UniPlusDbContext ctx) =>
        await ctx.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO configuracao.tipos_de_deficiencia
                (id, codigo, nome, descricao, created_at, is_deleted)
            VALUES ({0}, 'codigo_minusculo', {1}, 'Descrição de teste', now(), false)
            """,
            Guid.CreateVersion7(),
            NomeDePersistenciaUnico());

    private async Task PersistirAsync(TipoDeficiencia tipo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.TiposDeficiencia.Add(tipo);
        await ctx.SaveChangesAsync();
    }

    private async Task RemoverAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        TipoDeficiencia tracked = await ctx.TiposDeficiencia.SingleAsync(t => t.Id == id);
        ctx.TiposDeficiencia.Remove(tracked);
        await ctx.SaveChangesAsync();
    }

    private async Task VerificarTrilhaDeExclusaoAsync(Guid id, string codigo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        TipoDeficiencia excluido = await ctx.TiposDeficiencia.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedBy.Should().Be(AdminB);
        excluido.Codigo.Valor.Should().Be(codigo, "a trilha preserva o código do tipo removido");
    }

    private async Task PrepararListagemAsync(string nomeA, string nomeB, string nomeExcluido, string codigoA)
    {
        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDeficiencia.Add(Novo(CodigoUnico(), nomeB));
            ctx.TiposDeficiencia.Add(Novo(codigoA, nomeA));
            ctx.TiposDeficiencia.Add(Novo(CodigoUnico(), nomeExcluido));
            await ctx.SaveChangesAsync();
        }

        await RemoverPorNomeAsync(nomeExcluido);
    }

    private async Task RemoverPorNomeAsync(string nome)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        TipoDeficiencia aExcluir = await ctx.TiposDeficiencia.SingleAsync(t => t.Nome == nome);
        ctx.TiposDeficiencia.Remove(aExcluir);
        await ctx.SaveChangesAsync();
    }

    private static async Task VerificarLeituraPorIdAsync(UniPlusDbContext ctx, Guid id, string codigo, string nome)
    {
        var reader = new TipoDeficienciaReader(ctx);
        TipoDeficienciaView? view = await reader.ObterPorIdAsync(id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be(codigo, "o módulo Seleção congela código + origem (UNI-REQ-0061)");
        view.Nome.Should().Be(nome);
    }

    private static async Task VerificarViolacaoDeUnicidadeAsync(Func<Task> act, string restricao)
    {
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(restricao);
    }

    private static TipoDeficiencia Novo(string codigo, string nome) =>
        TipoDeficiencia.Criar(codigo, nome, "Descrição de teste").Value!;
}
