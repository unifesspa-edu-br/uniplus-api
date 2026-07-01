namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta da Modalidade contra Postgres real (UNI-REQ-0011):
/// persistência, UNIQUE parcial do código vivo, liberação do slot por soft-delete,
/// CHECKs de domínio (natureza, formato do código, coerência RETIRA_DE⟺origem),
/// bloqueio de remoção quando referenciada (composicao_origem e remanejamento_args
/// jsonb) e leitura cross-módulo ordenada por código.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ModalidadePersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public ModalidadePersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string codigo = CodigoUnico();
        Modalidade modalidade = Cota(codigo);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Modalidades.Add(modalidade);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Modalidade persistida = await readCtx.Modalidades.SingleAsync(m => m.Id == modalidade.Id);

        persistida.Codigo.Valor.Should().Be(codigo);
        persistida.NaturezaLegal.Should().Be(Domain.Enums.NaturezaLegal.CotaReservada);
        persistida.RegraRemanejamento.Should().Be(Domain.Enums.RegraRemanejamento.SegueCascata);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();

        var reader = new ModalidadeReader(readCtx);
        ModalidadeView? view = await reader.ObterPorIdAsync(modalidade.Id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be(codigo);
        view.NaturezaLegal.Should().Be("COTA_RESERVADA");
        view.RegraRemanejamento.Should().Be("SEGUE_CASCATA");
    }

    [Fact(DisplayName = "UNIQUE parcial do código rejeita segunda modalidade viva com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Modalidades.Add(Ampla(codigo));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Modalidades.Add(Ampla(codigo));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation) em CodigoJaExiste/409.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_modalidade_codigo_vivo");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_LiberaSlot()
    {
        string codigo = CodigoUnico();
        Modalidade modalidade = Ampla(codigo);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Modalidades.Add(modalidade);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            Modalidade tracked = await ctx.Modalidades.SingleAsync(m => m.Id == modalidade.Id);
            ctx.Modalidades.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            Modalidade excluida = await ctx.Modalidades
                .IgnoreQueryFilters().SingleAsync(m => m.Id == modalidade.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.Modalidades.Add(Ampla(codigo));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita natureza fora do domínio via SQL cru")]
    public async Task Check_RejeitaNaturezaForaDoDominioViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.modalidade (id, codigo, natureza_legal, composicao_vagas, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"INVALIDA"}, {"RESIDUAL_DO_VO"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de domínio de natureza_legal impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita código fora do formato via SQL cru")]
    public async Task Check_RejeitaCodigoForaDoFormatoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.modalidade (id, codigo, natureza_legal, composicao_vagas, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {"lb-ppi"}, {"AMPLA"}, {"RESIDUAL_DO_VO"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK codigo ~ '^[A-Z0-9_]+$' impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita RETIRA_DE sem origem via SQL cru")]
    public async Task Check_RejeitaRetiraDeSemOrigemViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.modalidade (id, codigo, natureza_legal, composicao_vagas, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"AMPLA"}, {"RETIRA_DE"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK (composicao_vagas = 'RETIRA_DE') = (composicao_origem IS NOT NULL) impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita origem preenchida sem RETIRA_DE via SQL cru")]
    public async Task Check_RejeitaOrigemSemRetiraDeViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.modalidade (id, codigo, natureza_legal, composicao_vagas, composicao_origem, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"AMPLA"}, {"RESIDUAL_DO_VO"}, {"AC"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência RETIRA_DE⟺origem impede o INSERT direto");
    }

    [Fact(DisplayName = "Remoção é bloqueada por referência via composicao_origem (repo)")]
    public async Task EhReferenciada_PorComposicaoOrigem_True()
    {
        string origem = CodigoUnico();
        string dependente = CodigoUnico();

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Modalidades.Add(Ampla(origem));
        ctx.Modalidades.Add(RetiraDe(dependente, origem));
        await ctx.SaveChangesAsync();

        var repo = new ModalidadeRepository(ctx);
        bool referenciada = await repo.EhReferenciadaPorOutraModalidadeVivaAsync(origem, ExcluirId(ctx, origem), default);

        referenciada.Should().BeTrue("outra modalidade viva aponta este código como composicao_origem");
    }

    [Fact(DisplayName = "Remoção é bloqueada por referência via remanejamento_args jsonb (repo)")]
    public async Task EhReferenciada_PorRemanejamentoArgs_True()
    {
        string destino = CodigoUnico();
        string dependente = CodigoUnico();

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Modalidades.Add(Ampla(destino));
        ctx.Modalidades.Add(DestinoUnico(dependente, destino));
        await ctx.SaveChangesAsync();

        var repo = new ModalidadeRepository(ctx);
        bool referenciada = await repo.EhReferenciadaPorOutraModalidadeVivaAsync(destino, ExcluirId(ctx, destino), default);

        referenciada.Should().BeTrue("o destino consta em remanejamento_args->>'destino' de outra viva");
    }

    [Fact(DisplayName = "Código não referenciado por ninguém não bloqueia a remoção (repo)")]
    public async Task EhReferenciada_SemReferencia_False()
    {
        string livre = CodigoUnico();

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.Modalidades.Add(Ampla(livre));
        await ctx.SaveChangesAsync();

        var repo = new ModalidadeRepository(ctx);
        bool referenciada = await repo.EhReferenciadaPorOutraModalidadeVivaAsync(livre, ExcluirId(ctx, livre), default);

        referenciada.Should().BeFalse();
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por código e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorCodigoEExcluiSoftDeleted()
    {
        string prefixo = $"MOD_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        string codA = $"{prefixo}_A";
        string codB = $"{prefixo}_B";
        string codExcluido = $"{prefixo}_D";

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.Modalidades.Add(Ampla(codB));
            ctx.Modalidades.Add(Ampla(codA));
            ctx.Modalidades.Add(Ampla(codExcluido));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CodigoModalidade voExcluido = CodigoModalidade.Criar(codExcluido).Value!;
            Modalidade aExcluir = await ctx.Modalidades.SingleAsync(m => m.Codigo == voExcluido);
            ctx.Modalidades.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new ModalidadeReader(readCtx);
        IReadOnlyList<ModalidadeView> todos = await reader.ListarVivosAsync();

        string[] meus = [.. todos
            .Select(v => v.Codigo)
            .Where(c => c.StartsWith(prefixo, StringComparison.Ordinal))];

        meus.Should().Equal([codA, codB]);
    }

    private static Guid ExcluirId(ConfiguracaoDbContext ctx, string codigo)
    {
        CodigoModalidade vo = CodigoModalidade.Criar(codigo).Value!;
        return ctx.Modalidades.Single(m => m.Codigo == vo).Id;
    }

    private static Modalidade Ampla(string codigo) =>
        Modalidade.Criar(codigo, null, "AMPLA", "RESIDUAL_DO_VO", null, null, null, null, null, null, null, null).Value!;

    private static Modalidade Cota(string codigo) =>
        Modalidade.Criar(codigo, "Cota", "COTA_RESERVADA", "DENTRO_DO_VR", null, "SEGUE_CASCATA", null, null, null, null, null, null).Value!;

    private static Modalidade RetiraDe(string codigo, string origem) =>
        Modalidade.Criar(codigo, null, "AMPLA", "RETIRA_DE", origem, null, null, null, null, null, null, null).Value!;

    private static Modalidade DestinoUnico(string codigo, string destino) =>
        Modalidade.Criar(codigo, null, "SUPLEMENTAR", "SUPLEMENTAR_AO_TOTAL", null, "DESTINO_UNICO", destino, null, null, null, null, null).Value!;

    private static string CodigoUnico() => $"MOD_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
