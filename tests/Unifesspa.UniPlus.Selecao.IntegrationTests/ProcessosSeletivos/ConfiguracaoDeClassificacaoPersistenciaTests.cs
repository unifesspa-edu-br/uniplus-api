namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeClassificacaoPersistida;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da
/// classificação (Story #775, bloco 15º): persiste e recarrega
/// <c>ConfiguracaoClassificacao</c> + <c>RegraEliminacao</c>, provando o
/// mapeamento EF (owned types de <c>ReferenciaRegra</c>, coluna <c>json</c>
/// — não <c>jsonb</c> — da união polimórfica <c>ArgsRegraEliminacao</c> para
/// as 4 variantes) contra Postgres real, e a reconfiguração sobre o agregado
/// tracked (mesma regressão de <c>ValueGeneratedNever</c> validada na F0).
/// </summary>
[Collection(ClassificacaoPersistenciaColecao.Nome)]
public sealed class ConfiguracaoDeClassificacaoPersistenciaTests
{
    private readonly CenarioDeClassificacaoPersistida _cenario;

    public ConfiguracaoDeClassificacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeClassificacaoPersistida(fixture);
    }

    [Fact(DisplayName = "O comentário de schema de baseado_em_enem não enumera as regras de eliminação")]
    public async Task ComentarioDeBaseadoEmEnem_NaoEnumeraAsRegras()
    {
        await using UniPlusDbContext context = _cenario.CriarDbContext();

        string? comentario = await context.Database.SqlQuery<string?>($"""
            SELECT col_description('selecao.configuracoes_classificacao'::regclass, a.attnum) AS "Value"
            FROM pg_attribute a
            WHERE a.attrelid = 'selecao.configuracoes_classificacao'::regclass AND a.attname = 'baseado_em_enem'
            """).SingleAsync();

        comentario.Should().Contain("regras de eliminação do ENEM").And.NotContain(
            "ELIM-", "um comentário que lista os códigos exigiria alterar a coluna a cada regra nova do ENEM");
    }

    [Fact(DisplayName = "Persiste e recarrega classificação com as 5 variantes de eliminação (prova a coluna json polimórfica)")]
    public async Task PersisteERecarrega_ComCincoVariantesDeEliminacao()
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU);
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDeEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        RegraEliminacao notaMinima = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "a"),
            new ArgsElimNotaMinimaEtapa(etapa.Id, 3m)).Value!;
        RegraEliminacao corteRedacao = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteEmArea, "b"),
            new ArgsElimCorteEmArea("REDACAO", 400m)).Value!;
        RegraEliminacao zeroEmArea = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimZeroEmArea, "c"),
            new ArgsElimZeroEmArea()).Value!;
        RegraEliminacao faltaEmDiaDeProva = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "0"),
            new ArgsElimFaltaEmDiaDeProvaEnem()).Value!;
        RegraEliminacao ausenciaNaEtapa = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, "7"),
            new ArgsElimAusenciaNaEtapa(etapa.Id)).Value!;

        Result<ConfiguracaoClassificacao> configResult = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "d"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "e"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "f"),
            [notaMinima, corteRedacao, zeroEmArea, faltaEmDiaDeProva, ausenciaNaEtapa],
            baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m);
        configResult.IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(configResult.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await _cenario.PersistirAsync(processo);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoClassificacao classificacao = recarregado!.Classificacao!;
        classificacao.RegraCalculo.Codigo.Should().Be(RegraCalculoCodigo.FormulaMediaPonderada);
        classificacao.RegraArredondamento!.Codigo.Should().Be(RegraArredondamentoCodigo.PrecisaoTruncar);
        classificacao.CasasArredondamento.Should().Be(2);
        classificacao.RegraOrdemAlocacao.Codigo.Should().Be(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria);
        classificacao.RegrasEliminacao.Should().HaveCount(5);

        ArgsElimNotaMinimaEtapa argsNotaMinima = (ArgsElimNotaMinimaEtapa)classificacao.RegrasEliminacao
            .Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimNotaMinimaEtapa).Args;
        argsNotaMinima.EtapaRef.Should().Be(etapa.Id);
        argsNotaMinima.NotaMinima.Should().Be(3m);

        ArgsElimCorteEmArea argsCorteRedacao = (ArgsElimCorteEmArea)classificacao.RegrasEliminacao
            .Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimCorteEmArea).Args;
        argsCorteRedacao.Minimo.Should().Be(400m);

        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimZeroEmArea)
            .Args.Should().BeOfType<ArgsElimZeroEmArea>();
        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem)
            .Args.Should().BeOfType<ArgsElimFaltaEmDiaDeProvaEnem>();

        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimAusenciaNaEtapa)
            .Args.Should().Be(new ArgsElimAusenciaNaEtapa(etapa.Id));

        string argsGravados = await readContext.Database.SqlQuery<string>($"""
            SELECT args::text AS "Value" FROM selecao.regras_eliminacao WHERE id = {faltaEmDiaDeProva.Id}
            """).SingleAsync();
        argsGravados.Should().Contain("\"$tipo\":\"faltaEmDiaDeProvaEnem\"",
            "o discriminador gravado é o que a leitura usa para voltar à variante certa");
    }

    [Fact(DisplayName = "Persiste e recarrega classificação CLASSIFICACAO-IMPORTADA sem arredondamento (INV-B8)")]
    public async Task PersisteERecarrega_Importada_SemArredondamento()
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — Transferência", TiposDeProcessoDeTeste.TransferenciaExterna);
        processo.DefinirEtapas([EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Análise curricular", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDeEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result<ConfiguracaoClassificacao> configResult = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, "1"),
            regraArredondamento: null,
            casasArredondamento: null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "2"),
            [],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: [], escalaNotaFinal: null);
        configResult.IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(configResult.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await _cenario.PersistirAsync(processo);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoClassificacao classificacao = recarregado!.Classificacao!;
        classificacao.RegraCalculo.Codigo.Should().Be(RegraCalculoCodigo.ClassificacaoImportada);
        classificacao.RegraArredondamento.Should().BeNull();
        classificacao.CasasArredondamento.Should().BeNull();
        classificacao.RegrasEliminacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Reconfigurar classificação sobre o agregado tracked insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarClassificacaoSobreAgregadoTracked_InsereFilhos()
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — PSVR", TiposDeProcessoDeTeste.PSVR);
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDeEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao original = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "b"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"),
            [],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!;
        processo.DefinirClassificacao(original, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await _cenario.PersistirAsync(processo);

        await using (UniPlusDbContext configureContext = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(configureContext);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            RegraEliminacao eliminacao = RegraEliminacao.Criar(
                Regra(RegraEliminacaoCodigo.ElimCorteEmArea, "d"),
                new ArgsElimCorteEmArea("REDACAO", 350m)).Value!;
            ConfiguracaoClassificacao nova = ConfiguracaoClassificacao.Criar(
                Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
                Regra(RegraArredondamentoCodigo.PrecisaoArredondarCima, "e"),
                4,
                Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"),
                [eliminacao],
                baseadoEmEnem: true,
                QuadroPesoAreaEnemDeTeste.Resolucao,
                QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!;

            Result result = carregado.DefinirClassificacao(nova, PrecondicaoIfMatch.Ausente);
            result.IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoClassificacao classificacao = recarregado!.Classificacao!;
        classificacao.CasasArredondamento.Should().Be(4);
        classificacao.RegrasEliminacao.Should().ContainSingle();
    }

    [Fact(DisplayName = "Atualizar dados da MESMA etapa (Id preservado) mantém a eliminação referenciando-a (F3)")]
    public async Task AtualizarEtapaMesmoId_MantemEliminacaoReferenciada()
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU);
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDeEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "a"), new ArgsElimNotaMinimaEtapa(etapa.Id, 3m)).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "c"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "d"),
            [eliminacao],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: [], escalaNotaFinal: 100m).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await _cenario.PersistirAsync(processo);

        await using (UniPlusDbContext configureContext = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(configureContext);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            // Reproduz a reconciliação do DefinirEtapasCommandHandler: o
            // cliente ecoa o Id lido anteriormente, o handler ATUALIZA a
            // mesma instância tracked em vez de recriá-la — sem isso, o
            // etapa_ref da eliminação ficaria órfão a cada PUT /etapas.
            EtapaProcesso etapaTracked = carregado.Etapas.Single();
            etapaTracked.AtualizarDados(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva (revisada)", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDeEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 2m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null));

            Result result = carregado.DefinirEtapas([etapaTracked], PrecondicaoIfMatch.Ausente);
            result.IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
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
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — BaseadoEmEnem", TiposDeProcessoDeTeste.PSIQ);
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDeEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!], PrecondicaoIfMatch.Ausente);

        Result<ConfiguracaoClassificacao> configResult = ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, "1"),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "2"),
            2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "3"),
            [],
            baseadoEmEnem: baseadoEmEnem,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
            baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : [], escalaNotaFinal: 100m);
        configResult.IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(configResult.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await _cenario.PersistirAsync(processo);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.Classificacao!.BaseadoEmEnem.Should().Be(baseadoEmEnem,
            "o valor persistido e recarregado do banco precisa sobreviver ao ciclo EF — o rótulo TipoProcesso " +
            "(PSIQ) não decide o valor");
    }
}
