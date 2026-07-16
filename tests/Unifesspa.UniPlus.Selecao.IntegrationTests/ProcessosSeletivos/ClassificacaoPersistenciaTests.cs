namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da
/// classificação (Story #775, bloco 15º): persiste e recarrega
/// <c>ConfiguracaoClassificacao</c> + <c>RegraEliminacao</c>, provando o
/// mapeamento EF (owned types de <c>ReferenciaRegra</c>, coluna <c>json</c>
/// — não <c>jsonb</c> — da união polimórfica <c>ArgsRegraEliminacao</c> para
/// as 3 variantes) contra Postgres real, e a reconfiguração sobre o agregado
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

    [Fact(DisplayName = "Persiste e recarrega classificação com as 3 variantes de eliminação (prova a coluna json polimórfica)")]
    public async Task PersisteERecarrega_ComTresVariantesDeEliminacao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        EtapaProcesso etapa = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
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

        Result<ConfiguracaoClassificacao> configResult = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "d"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "e"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "f"),
            1,
            [notaMinima, corteRedacao, zeroEmArea]);
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
        classificacao.RegrasEliminacao.Should().HaveCount(3);

        ArgsElimNotaMinimaEtapa argsNotaMinima = (ArgsElimNotaMinimaEtapa)classificacao.RegrasEliminacao
            .Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimNotaMinimaEtapa).Args;
        argsNotaMinima.EtapaRef.Should().Be(etapa.Id);
        argsNotaMinima.NotaMinima.Should().Be(3m);

        ArgsElimCorteRedacao argsCorteRedacao = (ArgsElimCorteRedacao)classificacao.RegrasEliminacao
            .Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimCorteRedacao).Args;
        argsCorteRedacao.Minimo.Should().Be(400m);

        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimZeroEmArea)
            .Args.Should().BeOfType<ArgsElimZeroEmArea>();
    }

    [Fact(DisplayName = "Persiste e recarrega classificação CLASSIFICACAO-IMPORTADA sem arredondamento (INV-B8)")]
    public async Task PersisteERecarrega_Importada_SemArredondamento()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Transferência", TipoProcesso.TransferenciaExterna, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar("Análise curricular", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result<ConfiguracaoClassificacao> configResult = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, "1"),
            regraArredondamento: null,
            casasArredondamento: null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "2"),
            2,
            []);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSVR", TipoProcesso.PSVR, OrigemCandidatos.InscricaoPropria);
        EtapaProcesso etapa = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao original = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "b"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            1,
            []).Value!;
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
                [eliminacao]).Value!;

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

    [Fact(DisplayName = "Atualizar dados da MESMA etapa (Id preservado) mantém a eliminação referenciando-a (achado Codex, F3)")]
    public async Task AtualizarEtapaMesmoId_MantemEliminacaoReferenciada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        EtapaProcesso etapa = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "a"), new ArgsElimNotaMinimaEtapa(etapa.Id, 3m)).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "d"),
            1,
            [eliminacao]).Value!;
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
            etapaTracked.AtualizarDados("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, 2m, null, 1);

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
}
