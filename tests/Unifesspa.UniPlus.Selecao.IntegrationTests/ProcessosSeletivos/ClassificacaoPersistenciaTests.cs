namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da
/// classificação (Story #775, bloco 15º): persiste e recarrega
/// <c>ConfiguracaoClassificacao</c> + <c>RegraEliminacao</c>, provando o
/// mapeamento EF (owned types de <c>ReferenciaRegra</c>, coluna <c>json</c>
/// — não <c>jsonb</c> — da união polimórfica <c>ArgsRegraEliminacao</c> para
/// as 4 variantes) contra Postgres real, e a reconfiguração sobre o agregado
/// tracked (mesma regressão de <c>ValueGeneratedNever</c> validada na F0).
/// </summary>
public sealed class ClassificacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public ClassificacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashChar[0], 64)).Value!;

    [Fact(DisplayName = "O comentário de schema de baseado_em_enem não enumera as regras de eliminação")]
    public async Task ComentarioDeBaseadoEmEnem_NaoEnumeraAsRegras()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        string? comentario = await context.Database.SqlQuery<string?>($"""
            SELECT col_description('selecao.configuracoes_classificacao'::regclass, a.attnum) AS "Value"
            FROM pg_attribute a
            WHERE a.attrelid = 'selecao.configuracoes_classificacao'::regclass AND a.attname = 'baseado_em_enem'
            """).SingleAsync();

        comentario.Should().Contain("regras de eliminação do ENEM").And.NotContain(
            "ELIM-", "um comentário que lista os códigos exigiria alterar a coluna a cada regra nova do ENEM");
    }

    [Fact(DisplayName = "Persiste e recarrega classificação com as 4 variantes de eliminação (prova a coluna json polimórfica)")]
    public async Task PersisteERecarrega_ComQuatroVariantesDeEliminacao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapa = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true).Value!, peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        RegraEliminacao notaMinima = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "a"),
            new ArgsElimNotaMinimaEtapa(etapa.Id, 3m)).Value!;
        RegraEliminacao corteRedacao = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteRedacao, "b"),
            new ArgsElimCorteRedacao(400m)).Value!;
        RegraEliminacao zeroEmArea = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimZeroEmArea, "c"),
            new ArgsElimZeroEmArea()).Value!;
        RegraEliminacao faltaEmDiaDeProva = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "0"),
            new ArgsElimFaltaEmDiaDeProvaEnem()).Value!;

        Result<ConfiguracaoClassificacao> configResult = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "d"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "e"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "f"),
            1,
            [notaMinima, corteRedacao, zeroEmArea, faltaEmDiaDeProva],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnemDeTeste.Completo());
        configResult.IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(configResult.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoClassificacao classificacao = recarregado!.Classificacao!;
        classificacao.RegraCalculo.Codigo.Should().Be(RegraCalculoCodigo.FormulaMediaPonderada);
        classificacao.RegraArredondamento!.Codigo.Should().Be(RegraArredondamentoCodigo.PrecisaoTruncar);
        classificacao.CasasArredondamento.Should().Be(2);
        classificacao.RegraOrdemAlocacao.Codigo.Should().Be(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04);
        classificacao.NOpcoesAlocacao.Should().Be(1);
        classificacao.RegrasEliminacao.Should().HaveCount(4);

        ArgsElimNotaMinimaEtapa argsNotaMinima = (ArgsElimNotaMinimaEtapa)classificacao.RegrasEliminacao
            .Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimNotaMinimaEtapa).Args;
        argsNotaMinima.EtapaRef.Should().Be(etapa.Id);
        argsNotaMinima.NotaMinima.Should().Be(3m);

        ArgsElimCorteRedacao argsCorteRedacao = (ArgsElimCorteRedacao)classificacao.RegrasEliminacao
            .Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimCorteRedacao).Args;
        argsCorteRedacao.Minimo.Should().Be(400m);

        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimZeroEmArea)
            .Args.Should().BeOfType<ArgsElimZeroEmArea>();
        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem)
            .Args.Should().BeOfType<ArgsElimFaltaEmDiaDeProvaEnem>();

        string argsGravados = await readContext.Database.SqlQuery<string>($"""
            SELECT args::text AS "Value" FROM selecao.regras_eliminacao WHERE id = {faltaEmDiaDeProva.Id}
            """).SingleAsync();
        argsGravados.Should().Contain("\"$tipo\":\"faltaEmDiaDeProvaEnem\"",
            "o discriminador gravado é o que a leitura usa para voltar à variante certa");
    }

    [Fact(DisplayName = "Persiste e recarrega classificação CLASSIFICACAO-IMPORTADA sem arredondamento (INV-B8)")]
    public async Task PersisteERecarrega_Importada_SemArredondamento()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Transferência", TipoProcesso.TransferenciaExterna, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas([EtapaProcesso.Criar("Análise curricular", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true).Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result<ConfiguracaoClassificacao> configResult = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, "1"),
            regraArredondamento: null,
            casasArredondamento: null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "2"),
            2,
            [],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []);
        configResult.IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(configResult.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoClassificacao classificacao = recarregado!.Classificacao!;
        classificacao.RegraCalculo.Codigo.Should().Be(RegraCalculoCodigo.ClassificacaoImportada);
        classificacao.RegraArredondamento.Should().BeNull();
        classificacao.CasasArredondamento.Should().BeNull();
        classificacao.NOpcoesAlocacao.Should().Be(2);
        classificacao.RegrasEliminacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Reconfigurar classificação sobre o agregado tracked insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarClassificacaoSobreAgregadoTracked_InsereFilhos()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSVR", TipoProcesso.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapa = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true).Value!, peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao original = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "b"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            1,
            [],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;
        processo.DefinirClassificacao(original, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext configureContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(configureContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            RegraEliminacao eliminacao = RegraEliminacao.Criar(
                Regra(RegraEliminacaoCodigo.ElimCorteRedacao, "d"),
                new ArgsElimCorteRedacao(350m)).Value!;
            ConfiguracaoClassificacao nova = ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
                Regra(RegraArredondamentoCodigo.PrecisaoArredondarCima, "e"),
                4,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
                2,
                [eliminacao],
                baseadoEmEnem: true,
                QuadroPesoAreaEnemDeTeste.Resolucao,
                QuadroPesoAreaEnemDeTeste.Completo()).Value!;

            Result result = carregado.DefinirClassificacao(nova, PrecondicaoIfMatch.Ausente);
            result.IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoClassificacao classificacao = recarregado!.Classificacao!;
        classificacao.CasasArredondamento.Should().Be(4);
        classificacao.NOpcoesAlocacao.Should().Be(2);
        classificacao.RegrasEliminacao.Should().ContainSingle();
    }

    [Fact(DisplayName = "Quadro de pesos por área congelado persiste e volta pelo carregamento do agregado, com peso e corte exatos")]
    public async Task QuadroPesoAreaEnem_PersisteERecarregaPeloAgregado()
    {
        ProcessoSeletivo processo = NovoProcessoComClassificacaoEnem();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo recarregado = (await new ProcessoSeletivoRepository(readContext, TimeProvider.System)
            .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

        ConfiguracaoClassificacao classificacao = recarregado.Classificacao!;
        classificacao.ResolucaoPesoAreaEnem.Should().Be(QuadroPesoAreaEnemDeTeste.Resolucao);
        classificacao.QuadroPesoAreaEnem.Select(g => g.GrupoAreaEnem.Codigo).Should().BeEquivalentTo(
            ["HUMANISTICA_I", "HUMANISTICA_II", "SAUDE_E_BIOLOGICAS", "TECNOLOGICA"]);

        GrupoPesoAreaEnemCongelado humanisticaI = classificacao.QuadroPesoAreaEnem.Single(g => g.GrupoAreaEnem.Codigo == "HUMANISTICA_I");
        humanisticaI.GrupoAreaEnem.Rotulo.Should().Be("Humanística I");
        humanisticaI.BaseLegal.Should().Be("Resolução nº 805/2024/Consepe – Anexo I");
        humanisticaI.Areas.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().BeEquivalentTo(
        [
            ("REDACAO", "Redação", 2.00m, (decimal?)400m),
            ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, (decimal?)null),
            ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, (decimal?)null),
            ("LINGUAGENS", "Linguagens e suas Tecnologias", 3.00m, (decimal?)null),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, (decimal?)null),
        ]);
    }

    [Fact(DisplayName = "Desmarcar BaseadoEmEnem descarta a resolução e o quadro congelado, sem grupo nem área órfãos")]
    public async Task DesmarcarBaseadoEmEnem_DescartaQuadroSemOrfaos()
    {
        ProcessoSeletivo processo = NovoProcessoComClassificacaoEnem();
        Guid classificacaoOriginalId = processo.Classificacao!.Id;
        Guid[] gruposOriginais = [.. processo.Classificacao.QuadroPesoAreaEnem.Select(g => g.Id)];

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        (await ContarGruposAsync(classificacaoOriginalId)).Should().Be(4);
        (await ContarAreasAsync(gruposOriginais)).Should().Be(20);

        await using (SelecaoDbContext configureContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(configureContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            ConfiguracaoClassificacao semEnem = ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
                Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "b"),
                2,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
                1,
                [],
                baseadoEmEnem: false,
                resolucaoPesoAreaEnem: null,
                quadroPesoAreaEnem: []).Value!;
            carregado.DefinirClassificacao(semEnem, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext readContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivo recarregado = (await new ProcessoSeletivoRepository(readContext, TimeProvider.System)
                .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            recarregado.Classificacao!.BaseadoEmEnem.Should().BeFalse();
            recarregado.Classificacao.ResolucaoPesoAreaEnem.Should().BeNull();
            recarregado.Classificacao.QuadroPesoAreaEnem.Should().BeEmpty();
        }

        (await ContarGruposAsync(classificacaoOriginalId)).Should().Be(0, "o quadro vai embora com a classificação que o congelou");
        (await ContarAreasAsync(gruposOriginais)).Should().Be(0, "as áreas vão embora com o grupo");
    }

    [Fact(DisplayName = "O banco recusa o mesmo grupo duas vezes no quadro de uma classificação")]
    public async Task QuadroPesoAreaEnem_GrupoRepetidoNaMesmaClassificacao_BancoRecusa()
    {
        GrupoPesoAreaEnemCongelado grupo = await PersistirEObterUmGrupoAsync();

        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        Func<Task> duplicar = () => ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO selecao.grupos_peso_area_enem_congelados (id, configuracao_classificacao_id, grupo_area_enem_codigo, grupo_area_enem_rotulo, base_legal, created_at) VALUES ({Guid.CreateVersion7()}, {grupo.ConfiguracaoClassificacaoId}, {grupo.GrupoAreaEnem.Codigo}, {grupo.GrupoAreaEnem.Rotulo}, {grupo.BaseLegal}, {DateTimeOffset.UtcNow})");

        (await duplicar.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Theory(DisplayName = "O banco recusa peso negativo e corte fora de 0 a 1000 nas áreas do quadro")]
    [InlineData(-0.0001, null)]
    [InlineData(1.0, -0.0001)]
    [InlineData(1.0, 1000.0001)]
    public async Task QuadroPesoAreaEnem_ValorForaDaFaixa_BancoRecusa(double peso, double? corte)
    {
        GrupoPesoAreaEnemCongelado grupo = await PersistirEObterUmGrupoAsync();

        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        Func<Task> inserir = () => ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO selecao.areas_peso_area_enem_congeladas (id, grupo_peso_area_enem_congelado_id, codigo, rotulo, peso, corte, created_at) VALUES ({Guid.CreateVersion7()}, {grupo.Id}, {"AREA_EXTRA"}, {"Área extra"}, {(decimal)peso}, {(decimal?)corte}, {DateTimeOffset.UtcNow})");

        (await inserir.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    private async Task<GrupoPesoAreaEnemCongelado> PersistirEObterUmGrupoAsync()
    {
        ProcessoSeletivo processo = NovoProcessoComClassificacaoEnem();
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            await new ProcessoSeletivoRepository(writeContext, TimeProvider.System).AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        return processo.Classificacao!.QuadroPesoAreaEnem.First();
    }

    private static ProcessoSeletivo NovoProcessoComClassificacaoEnem()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2027 — Medicina", TipoProcesso.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "b"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            1,
            [],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnemDeTeste.Completo()).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private async Task<int> ContarGruposAsync(Guid classificacaoId)
    {
        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        return await ctx.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM selecao.grupos_peso_area_enem_congelados WHERE configuracao_classificacao_id = {classificacaoId}")
            .SingleAsync();
    }

    private async Task<int> ContarAreasAsync(IEnumerable<Guid> grupoIds)
    {
        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        int total = 0;
        foreach (Guid grupoId in grupoIds)
        {
            total += await ctx.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM selecao.areas_peso_area_enem_congeladas WHERE grupo_peso_area_enem_congelado_id = {grupoId}")
                .SingleAsync();
        }

        return total;
    }

    [Fact(DisplayName = "Atualizar dados da MESMA etapa (Id preservado) mantém a eliminação referenciando-a (F3)")]
    public async Task AtualizarEtapaMesmoId_MantemEliminacaoReferenciada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapa = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true).Value!, peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "a"), new ArgsElimNotaMinimaEtapa(etapa.Id, 3m)).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "d"),
            1,
            [eliminacao],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (SelecaoDbContext configureContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(configureContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            // Reproduz a reconciliação do DefinirEtapasCommandHandler: o
            // cliente ecoa o Id lido anteriormente, o handler ATUALIZA a
            // mesma instância tracked em vez de recriá-la — sem isso, o
            // etapa_ref da eliminação ficaria órfão a cada PUT /etapas.
            EtapaProcesso etapaTracked = carregado.Etapas.Single();
            etapaTracked.AtualizarDados("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true).Value!, 2m, null, 1);

            Result result = carregado.DefinirEtapas([etapaTracked], PrecondicaoIfMatch.Ausente);
            result.IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        EtapaProcesso etapaAtualizada = recarregado!.Etapas.Single();
        etapaAtualizada.Id.Should().Be(etapa.Id);
        etapaAtualizada.Nome.Should().Be("Prova Objetiva (revisada)");

        RegraEliminacao eliminacaoRecarregada = recarregado.Classificacao!.RegrasEliminacao.Single();
        ((ArgsElimNotaMinimaEtapa)eliminacaoRecarregada.Args).EtapaRef.Should().Be(etapa.Id);
    }

    [Theory(DisplayName = "Persiste e recarrega BaseadoEmEnem — true e false (#850)")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PersisteERecarrega_BaseadoEmEnem(bool baseadoEmEnem)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — BaseadoEmEnem", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true).Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Ausente);

        Result<ConfiguracaoClassificacao> configResult = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "1"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "2"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "3"),
            1,
            [],
            baseadoEmEnem: baseadoEmEnem,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : []);
        configResult.IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(configResult.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.Classificacao!.BaseadoEmEnem.Should().Be(baseadoEmEnem,
            "o valor persistido e recarregado do banco precisa sobreviver ao ciclo EF — o rótulo TipoProcesso " +
            "(PSIQ) não decide o valor");
    }
}
