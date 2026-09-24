namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A migration que congela a resolução de Pesos por Área na classificação deixa o banco
/// coerente com a invariante nova: a classificação baseada em ENEM com cálculo local gravada
/// sem resolução nem quadro volta a "não definida", e as demais ficam.
/// </summary>
/// <remarks>
/// O estado anterior é produzido pelo próprio caminho da migration: grava-se no schema de hoje
/// e desce-se até a migration anterior, que remove resolução e quadro — exatamente a linha que
/// existia antes dela. Sem a limpeza, essa linha publicaria um envelope que o decodificador
/// recusa ao reidratar.
/// </remarks>
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class ClassificacaoEnemSemResolucaoMigrationTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260923221757_CongelaCodigoERotuloDoGrupoDeAreaEnem";

    // A descida e a subida passam pela migration que renomeia a regra de alocação, e ela descarta a
    // classificação em rascunho que cita o código antigo ou o novo. Um código fora do rol deixa as
    // classificações deste teste intocadas por ela.
    private const string AlocacaoForaDoRol = "ALOCACAO-FORA-DO-ROL";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_classificacao_enem_sem_resolucao_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "A migration devolve a 'não definida' a classificação ENEM com cálculo local sem resolução, com as regras de eliminação dela, e mantém as demais")]
    public async Task Migration_RemoveClassificacaoEnemLocalSemResolucao()
    {
        ProcessoSeletivo enemLocal = Processo("PS ENEM local");
        ConfiguracaoClassificacao classificacaoEnemLocal = Classificacao(
            RegraCalculoCodigo.FormulaMediaPonderada, baseadoEmEnem: true,
            [RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimZeroEmArea, 'e'), new ArgsElimZeroEmArea()).Value!]);
        enemLocal.DefinirClassificacao(classificacaoEnemLocal, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Guid regraEliminacaoId = classificacaoEnemLocal.RegrasEliminacao.Single().Id;

        ProcessoSeletivo enemImportada = Processo("PS ENEM importada");
        ConfiguracaoClassificacao classificacaoEnemImportada = Classificacao(RegraCalculoCodigo.ClassificacaoImportada, baseadoEmEnem: true, []);
        enemImportada.DefinirClassificacao(classificacaoEnemImportada, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ProcessoSeletivo localSemEnem = Processo("PS local sem ENEM");
        ConfiguracaoClassificacao classificacaoLocalSemEnem = Classificacao(RegraCalculoCodigo.FormulaMediaPonderada, baseadoEmEnem: false, []);
        localSemEnem.DefinirClassificacao(classificacaoLocalSemEnem, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync();
            ProcessoSeletivoRepository repositorio = new(contexto, TimeProvider.System);
            await repositorio.AdicionarAsync(enemLocal, CancellationToken.None);
            await repositorio.AdicionarAsync(enemImportada, CancellationToken.None);
            await repositorio.AdicionarAsync(localSemEnem, CancellationToken.None);
            await contexto.SaveChangesAsync();
        }

        // Desce até antes da resolução: a classificação ENEM com cálculo local fica como era
        // gravada até aqui, sem resolução e sem quadro.
        await using (SelecaoDbContext contexto = CriarContexto())
        {
            await contexto.GetService<IMigrator>().MigrateAsync(MigrationAnterior);
        }

        (await ContarPorIdAsync("configuracoes_classificacao", classificacaoEnemLocal.Id)).Should().Be(1,
            "pré-condição: sem a migration, a linha legada está lá");

        await using (SelecaoDbContext contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync();
        }

        (await ContarPorIdAsync("configuracoes_classificacao", classificacaoEnemLocal.Id)).Should().Be(0,
            "a classificação ENEM com cálculo local sem resolução volta a 'não definida', para o passo ser redefinido");
        (await ContarPorIdAsync("regras_eliminacao", regraEliminacaoId)).Should().Be(0,
            "as regras de eliminação vão junto com a classificação, sem órfã");
        (await ContarPorIdAsync("processos_seletivos", enemLocal.Id)).Should().Be(1,
            "só a classificação sai; o processo fica");
        (await ContarPorIdAsync("configuracoes_classificacao", classificacaoEnemImportada.Id)).Should().Be(1,
            "a classificação importada não usa pesos por área");
        (await ContarPorIdAsync("configuracoes_classificacao", classificacaoLocalSemEnem.Id)).Should().Be(1,
            "a classificação que não é baseada em ENEM não usa pesos por área");
    }

    private static ProcessoSeletivo Processo(string nome) =>
        ProcessoSeletivo.Criar(
            nome, TipoProcesso.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static ConfiguracaoClassificacao Classificacao(
        string regraCalculo, bool baseadoEmEnem, IReadOnlyList<RegraEliminacao> regrasEliminacao)
    {
        bool importada = regraCalculo == RegraCalculoCodigo.ClassificacaoImportada;
        bool exigeQuadro = ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem(Regra(regraCalculo, 'a'), baseadoEmEnem);

        return ConfiguracaoClassificacao.Criar(
            Regra(regraCalculo, 'a'),
            importada ? null : Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            importada ? null : 2,
            Regra(AlocacaoForaDoRol, 'c'),
            1,
            regrasEliminacao,
            baseadoEmEnem,
            exigeQuadro ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            exigeQuadro ? QuadroPesoAreaEnemDeTeste.Completo() : []).Value!;
    }

    private static ReferenciaRegra Regra(string codigo, char hash) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hash, 64)).Value!;

    private SelecaoDbContext CriarContexto()
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System, userContext: null),
                new AuditableInterceptor(TimeProvider.System, userContext: null))
            .Options;

        return new SelecaoDbContext(options);
    }

    private async Task<long> ContarPorIdAsync(string tabela, Guid id)
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
#pragma warning disable CA2100 // A tabela é literal do próprio teste; o valor vai por parâmetro.
        await using NpgsqlCommand comando = new($"SELECT count(*) FROM selecao.{tabela} WHERE id = @id", conexao);
#pragma warning restore CA2100
        comando.Parameters.AddWithValue("id", id);
        return (long)(await comando.ExecuteScalarAsync())!;
    }
}
