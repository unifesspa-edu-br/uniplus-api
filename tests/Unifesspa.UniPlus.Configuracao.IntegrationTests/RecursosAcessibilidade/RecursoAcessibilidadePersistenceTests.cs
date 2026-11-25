namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.RecursosAcessibilidade;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.RecursosAcessibilidade.DadosDeRecursoAcessibilidade;

/// <summary>
/// Integração ponta-a-ponta do RecursoAcessibilidade contra Postgres real
/// (UNI-REQ-0012): persistência, UNIQUE parcial do código e do nome vivos, liberação do slot por
/// soft-delete e leitura cross-módulo ordenada por nome.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class RecursoAcessibilidadePersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public RecursoAcessibilidadePersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string nome = NomeUnico();
        string codigo = CodigoUnico();
        RecursoAcessibilidade recurso = Novo(nome, codigo);

        await SalvarRecursos(recurso);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        RecursoAcessibilidade persistido = await readCtx.RecursosAcessibilidade.SingleAsync(r => r.Id == recurso.Id);

        ConferirRecursoPersistido(persistido, codigo, nome);

        await ConferirRecursoNoReader(readCtx, recurso.Id, codigo, nome);
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segundo recurso vivo com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await SalvarRecursos(Novo(NomeUnico(), codigo));

        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.RecursosAcessibilidade.Add(Novo(NomeUnico(), codigo));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        await ConferirViolacaoDeUnicidade(act, RestricoesDoBanco.RecursoDeAcessibilidadeCodigoAtivos);
    }

    [Fact(DisplayName = "UNIQUE parcial do nome rejeita segundo recurso vivo com mesmo nome")]
    public async Task UniquePartial_Nome_RejeitaDuplicataAtiva()
    {
        string nome = NomeUnico();
        await SalvarRecursos(Novo(nome));

        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.RecursosAcessibilidade.Add(Novo(nome));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        await ConferirViolacaoDeUnicidade(act, RestricoesDoBanco.RecursoDeAcessibilidadeNomeAtivos);
    }

    [Fact(DisplayName = "Nome distinto é aceito")]
    public async Task NomeDistinto_Aceita()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.RecursosAcessibilidade.Add(Novo(NomeUnico()));
        ctx.RecursosAcessibilidade.Add(Novo(NomeUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("os nomes são distintos");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do nome")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string nome = NomeUnico();
        RecursoAcessibilidade recurso = Novo(nome);
        await SalvarRecursos(recurso);

        await ExcluirRecurso(recurso.Id);
        await ConferirTrilhaDeExclusao(recurso.Id);

        await using UniPlusDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.RecursosAcessibilidade.Add(Novo(nome));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do nome foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por nome e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorNomeEExcluiSoftDeleted()
    {
        string prefixo = $"REC_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        string nomeA = $"{prefixo}_A";
        string nomeB = $"{prefixo}_B";
        string nomeExcluido = $"{prefixo}_D";

        await SalvarRecursos(Novo(nomeB), Novo(nomeA), Novo(nomeExcluido));
        await ExcluirRecursoPorNome(nomeExcluido);

        await ConferirOrdemDosRecursosVivos(prefixo, nomeA, nomeB);
    }

    private static RecursoAcessibilidade Novo(string nome, string? codigo = null) =>
        RecursoAcessibilidade.Criar(codigo ?? CodigoUnico(), nome, "Recurso de acessibilidade").Value!;

    private async Task ConferirOrdemDosRecursosVivos(string prefixo, string nomeA, string nomeB)
    {
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new RecursoAcessibilidadeReader(readCtx);
        IReadOnlyList<RecursoAcessibilidadeView> todos = await reader.ListarVivosAsync();
        string[] meus = [.. todos
            .Select(v => v.Nome)
            .Where(n => n.StartsWith(prefixo, StringComparison.Ordinal))];
        meus.Should().Equal([nomeA, nomeB]);
    }

    private async Task SalvarRecursos(params RecursoAcessibilidade[] recursos)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.RecursosAcessibilidade.AddRange(recursos);
        await ctx.SaveChangesAsync();
    }

    private async Task ExcluirRecurso(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        RecursoAcessibilidade recurso = await ctx.RecursosAcessibilidade.SingleAsync(r => r.Id == id);
        await RemoverRecurso(ctx, recurso);
    }

    private async Task ExcluirRecursoPorNome(string nome)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        RecursoAcessibilidade recurso = await ctx.RecursosAcessibilidade.SingleAsync(r => r.Nome == nome);
        await RemoverRecurso(ctx, recurso);
    }

    private static async Task RemoverRecurso(UniPlusDbContext ctx, RecursoAcessibilidade recurso)
    {
        ctx.RecursosAcessibilidade.Remove(recurso);
        await ctx.SaveChangesAsync();
    }

    private async Task ConferirTrilhaDeExclusao(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        RecursoAcessibilidade excluido = await ctx.RecursosAcessibilidade
            .IgnoreQueryFilters().SingleAsync(r => r.Id == id);
        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedBy.Should().Be(AdminB);
    }

    private static void ConferirRecursoPersistido(RecursoAcessibilidade recurso, string codigo, string nome)
    {
        recurso.Codigo.Valor.Should().Be(codigo);
        recurso.Nome.Should().Be(nome);
        recurso.CreatedBy.Should().Be(AdminA);
        recurso.IsDeleted.Should().BeFalse();
    }

    private static async Task ConferirRecursoNoReader(UniPlusDbContext ctx, Guid id, string codigo, string nome)
    {
        var reader = new RecursoAcessibilidadeReader(ctx);
        RecursoAcessibilidadeView? view = await reader.ObterPorIdAsync(id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be(codigo);
        view.Nome.Should().Be(nome);
    }

    private static async Task ConferirViolacaoDeUnicidade(Func<Task> salvar, string restricao)
    {
        DbUpdateException ex = (await salvar.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(restricao);
    }
}
