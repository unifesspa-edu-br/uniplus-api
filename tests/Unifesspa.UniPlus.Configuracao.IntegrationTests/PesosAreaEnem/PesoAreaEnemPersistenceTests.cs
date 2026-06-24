namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta dos Pesos do ENEM por grupo de área contra Postgres
/// real (UNI-REQ-0066): persistência dos pesos, UNIQUE parcial do par
/// (resolução, grupo), liberação do slot por soft-delete, CHECKs de domínio/não-
/// negatividade, DEFAULT 400 do corte e leitura cross-módulo (CA-01, CA-04, CA-05).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PesoAreaEnemPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";
    private const string BaseLegal = "Res. 805/2024 Anexo I";

    private readonly ConfiguracaoDbFixture _fixture;

    public PesoAreaEnemPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: criar persiste os pesos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem peso = Nova(resolucao);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);

        persistida.Resolucao.Should().Be(resolucao);
        persistida.GrupoCurso.Valor.Should().Be(GrupoCurso.Tecnologica);
        persistida.PesoRedacao.Should().Be(1.50m);
        persistida.PesoMatematica.Should().Be(2.00m);
        persistida.CorteRedacao.Should().Be(400m);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();

        var reader = new PesoAreaEnemReader(readCtx);
        PesoAreaEnemView? view = await reader.ObterPorIdAsync(peso.Id);
        view.Should().NotBeNull();
        view!.Resolucao.Should().Be(resolucao);
        view.GrupoCurso.Should().Be(GrupoCurso.Tecnologica);
        view.PesoRedacao.Should().Be(1.50m);
    }

    [Fact(DisplayName = "CA-02: UNIQUE parcial (resolução, grupo) rejeita segundo par vivo idêntico")]
    public async Task UniquePartial_Par_RejeitaDuplicataAtiva()
    {
        string resolucao = ResolucaoUnica();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(Nova(resolucao));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.PesosAreaEnem.Add(Nova(resolucao));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsParConflict) em
        // ParJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_peso_area_enem_resolucao_grupo_vivo");
    }

    [Fact(DisplayName = "Mesma resolução em grupo distinto é aceita (par distinto)")]
    public async Task ParDistinto_MesmaResolucaoOutroGrupo_Aceita()
    {
        string resolucao = ResolucaoUnica();
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.PesosAreaEnem.Add(Nova(resolucao, GrupoCurso.Tecnologica));
        ctx.PesosAreaEnem.Add(Nova(resolucao, GrupoCurso.HumanisticaI));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("o par (resolução, grupo) é distinto");
    }

    [Fact(DisplayName = "CA-05: soft-delete preserva a trilha e liberta o slot da UNIQUE parcial do par")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem peso = Nova(resolucao);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await ctx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            ctx.PesosAreaEnem.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            PesoAreaEnem excluida = await ctx.PesosAreaEnem
                .IgnoreQueryFilters().SingleAsync(p => p.Id == peso.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.PesosAreaEnem.Add(Nova(resolucao));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do par foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CHECK de banco rejeita peso negativo via SQL cru")]
    public async Task Check_RejeitaPesoNegativoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO peso_area_enem (id, resolucao, grupo_curso, peso_redacao, peso_ciencias_natureza, peso_ciencias_humanas, peso_linguagens, peso_matematica, corte_redacao, base_legal, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {ResolucaoUnica()}, {GrupoCurso.Tecnologica}, {-1.0m}, {1.0m}, {1.0m}, {1.0m}, {2.0m}, {400.0m}, {BaseLegal}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK peso_redacao >= 0 impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita grupo fora do domínio via SQL cru")]
    public async Task Check_RejeitaGrupoForaDoDominioViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO peso_area_enem (id, resolucao, grupo_curso, peso_redacao, peso_ciencias_natureza, peso_ciencias_humanas, peso_linguagens, peso_matematica, corte_redacao, base_legal, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {ResolucaoUnica()}, {"Engenharias"}, {1.5m}, {1.0m}, {1.0m}, {1.0m}, {2.0m}, {400.0m}, {BaseLegal}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de domínio de grupo_curso impede o INSERT direto");
    }

    [Fact(DisplayName = "CA-04: DEFAULT 400 do banco aplica quando o corte é omitido no INSERT cru")]
    public async Task Default_CorteRedacao_AplicaQuandoOmitido()
    {
        Guid id = Guid.CreateVersion7();
        string resolucao = ResolucaoUnica();

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            await ctx.Database.ExecuteSqlAsync(
                $"INSERT INTO peso_area_enem (id, resolucao, grupo_curso, peso_redacao, peso_ciencias_natureza, peso_ciencias_humanas, peso_linguagens, peso_matematica, base_legal, created_at, is_deleted) VALUES ({id}, {resolucao}, {GrupoCurso.Tecnologica}, {1.5m}, {1.0m}, {1.0m}, {1.0m}, {2.0m}, {BaseLegal}, {DateTimeOffset.UtcNow}, {false})");
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == id);
        persistida.CorteRedacao.Should().Be(400m);
    }

    [Fact(DisplayName = "Corte de redação no máximo (1000) persiste — numeric(7,3) acomoda a nota máxima do ENEM")]
    public async Task CorteRedacaoMaximo_Persiste()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem peso = Nova(resolucao, corte: PesoAreaEnem.CorteRedacaoMaximo);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.CorteRedacao.Should().Be(1000m);
    }

    [Fact(DisplayName = "Corte de redação 0 explícito via EF persiste 0 — o DEFAULT 400 do banco não sobrescreve")]
    public async Task CorteRedacaoZero_ViaEf_NaoEhSobrescritoPeloDefault()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem peso = Nova(resolucao, corte: 0m);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.CorteRedacao.Should().Be(0m);
    }

    [Fact(DisplayName = "CHECK de banco rejeita corte de redação acima de 1000 via SQL cru")]
    public async Task Check_RejeitaCorteAcimaDoMaximoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO peso_area_enem (id, resolucao, grupo_curso, peso_redacao, peso_ciencias_natureza, peso_ciencias_humanas, peso_linguagens, peso_matematica, corte_redacao, base_legal, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {ResolucaoUnica()}, {GrupoCurso.Tecnologica}, {1.5m}, {1.0m}, {1.0m}, {1.0m}, {2.0m}, {1000.001m}, {BaseLegal}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK corte_redacao <= 1000 impede o INSERT direto");
    }

    [Fact(DisplayName = "Reader.ListarVivasAsync ordena por resolução e exclui soft-deleted")]
    public async Task ListarVivas_OrdenaPorResolucaoEExcluiSoftDeleted()
    {
        // Prefixo único por execução: o banco é compartilhado na collection, então
        // filtramos o resultado às linhas deste teste para asserções determinísticas.
        string prefixo = $"Res. {Guid.NewGuid().ToString("N")[..12]}";
        string resA = $"{prefixo}-a";
        string resB = $"{prefixo}-b";
        string resExcluida = $"{prefixo}-d";

        // Insere fora de ordem (B antes de A) para provar a ordenação do reader.
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(Nova(resB));
            ctx.PesosAreaEnem.Add(Nova(resA));
            ctx.PesosAreaEnem.Add(Nova(resExcluida));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem aExcluir = await ctx.PesosAreaEnem.SingleAsync(p => p.Resolucao == resExcluida);
            ctx.PesosAreaEnem.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new PesoAreaEnemReader(readCtx);
        IReadOnlyList<PesoAreaEnemView> todas = await reader.ListarVivasAsync();

        string[] meus = [.. todas
            .Select(v => v.Resolucao)
            .Where(r => r.StartsWith(prefixo, StringComparison.Ordinal))];

        // O reader ordena por Resolucao ascendente e exclui o soft-deleted:
        // exatamente [resA, resB], nessa ordem (inserimos B antes de A).
        meus.Should().Equal([resA, resB]);
    }

    private static PesoAreaEnem Nova(string resolucao, string grupo = GrupoCurso.Tecnologica, decimal? corte = 400m) =>
        PesoAreaEnem.Criar(resolucao, grupo, 1.50m, 1.00m, 1.00m, 1.00m, 2.00m, corte, BaseLegal).Value!;

    private static string ResolucaoUnica() => $"Res. {Guid.NewGuid().ToString("N")[..12]}";
}
