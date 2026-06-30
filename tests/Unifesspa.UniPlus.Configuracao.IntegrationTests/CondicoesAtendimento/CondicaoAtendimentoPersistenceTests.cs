namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CondicoesAtendimento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Integração ponta-a-ponta da CondicaoAtendimentoEspecializado contra Postgres
/// real (UNI-REQ-0012): persistência, UNIQUE parcial do código vivo, liberação do
/// slot por soft-delete, CHECK de formato do código, bloqueio de remoção do código
/// reservado PCD (via handler) e leitura cross-módulo ordenada.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CondicaoAtendimentoPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public CondicaoAtendimentoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string codigo = CodigoUnico();
        CondicaoAtendimentoEspecializado condicao = Nova(codigo);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CondicoesAtendimento.Add(condicao);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        CondicaoAtendimentoEspecializado persistida = await readCtx.CondicoesAtendimento.SingleAsync(c => c.Id == condicao.Id);

        persistida.Codigo.Valor.Should().Be(codigo);
        persistida.Nome.Should().Be("Dislexia");
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();

        var reader = new CondicaoAtendimentoReader(readCtx);
        CondicaoAtendimentoView? view = await reader.ObterPorIdAsync(condicao.Id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be(codigo);
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segunda condição viva com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CondicoesAtendimento.Add(Nova(codigo));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.CondicoesAtendimento.Add(Nova(codigo));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsCodigoConflict) em
        // CodigoJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_condicao_atendimento_especializado_codigo_vivo");
    }

    [Fact(DisplayName = "Código distinto é aceito")]
    public async Task CodigoDistinto_Aceita()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.CondicoesAtendimento.Add(Nova(CodigoUnico()));
        ctx.CondicoesAtendimento.Add(Nova(CodigoUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("os códigos são distintos");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string codigo = CodigoUnico();
        CondicaoAtendimentoEspecializado condicao = Nova(codigo);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CondicoesAtendimento.Add(condicao);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CondicaoAtendimentoEspecializado tracked = await ctx.CondicoesAtendimento.SingleAsync(c => c.Id == condicao.Id);
            ctx.CondicoesAtendimento.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            CondicaoAtendimentoEspecializado excluida = await ctx.CondicoesAtendimento
                .IgnoreQueryFilters().SingleAsync(c => c.Id == condicao.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.CondicoesAtendimento.Add(Nova(codigo));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do formato fechado via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoFormatoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.condicao_atendimento_especializado (id, codigo, nome, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"pcd"}, {"X"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de formato do código (UPPER_SNAKE iniciando por letra) impede o INSERT direto");
    }

    [Fact(DisplayName = "Remover o código reservado PCD via handler é bloqueado e não exclui o registro")]
    public async Task Remover_Pcd_ViaHandler_Bloqueia()
    {
        CondicaoAtendimentoEspecializado pcd = Nova(CodigoCondicao.Pcd, nome: "Pessoa com deficiência");
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CondicoesAtendimento.Add(pcd);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext handlerCtx = _fixture.CreateDbContext(AdminB))
        {
            var repository = new CondicaoAtendimentoRepository(handlerCtx);
            Result resultado = await RemoverCondicaoAtendimentoCommandHandler.Handle(
                new RemoverCondicaoAtendimentoCommand(pcd.Id), repository, handlerCtx, CancellationToken.None);

            resultado.IsFailure.Should().BeTrue();
            resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.RemocaoBloqueadaCodigoProtegido);
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        bool aindaVivo = await readCtx.CondicoesAtendimento.AnyAsync(c => c.Id == pcd.Id);
        aindaVivo.Should().BeTrue("a condição reservada PCD não pode ser removida");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por código e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorCodigoEExcluiSoftDeleted()
    {
        string prefixo = $"COND_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        string codA = $"{prefixo}_A";
        string codB = $"{prefixo}_B";
        string codExcluido = $"{prefixo}_D";

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.CondicoesAtendimento.Add(Nova(codB));
            ctx.CondicoesAtendimento.Add(Nova(codA));
            ctx.CondicoesAtendimento.Add(Nova(codExcluido));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CodigoCondicao codigoExcluidoVo = CodigoCondicao.Criar(codExcluido).Value!;
            CondicaoAtendimentoEspecializado aExcluir = await ctx.CondicoesAtendimento.SingleAsync(c => c.Codigo == codigoExcluidoVo);
            ctx.CondicoesAtendimento.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new CondicaoAtendimentoReader(readCtx);
        IReadOnlyList<CondicaoAtendimentoView> todos = await reader.ListarVivosAsync();

        string[] meus = [.. todos
            .Select(v => v.Codigo)
            .Where(c => c.StartsWith(prefixo, StringComparison.Ordinal))];

        meus.Should().Equal([codA, codB]);
    }

    private static CondicaoAtendimentoEspecializado Nova(string codigo, string nome = "Dislexia") =>
        CondicaoAtendimentoEspecializado.Criar(codigo, nome, null).Value!;

    private static string CodigoUnico() => $"COND_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
