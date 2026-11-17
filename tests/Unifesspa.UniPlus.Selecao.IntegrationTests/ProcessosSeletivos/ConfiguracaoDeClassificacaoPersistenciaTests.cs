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
/// A classificação e as cinco variantes dos parâmetros de eliminação sobrevivem à gravação e à leitura
/// no PostgreSQL, inclusive quando o agregado rastreado recebe novas regras ou atualiza a mesma etapa.
/// </summary>
[Collection(ClassificacaoPersistenciaColecao.Nome)]
public sealed class ConfiguracaoDeClassificacaoPersistenciaTests
{
    private readonly CenarioDeClassificacaoPersistida _cenario;

    public ConfiguracaoDeClassificacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _cenario = new CenarioDeClassificacaoPersistida(fixture);

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
        ProcessoSeletivo processo = ProcessoComEtapa("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU);
        Guid etapaId = processo.Etapas.Single().Id;
        ConfiguracaoClassificacao configuracao = ClassificacaoComCincoVariantes(etapaId);
        processo.DefinirClassificacao(configuracao).IsSuccess.Should().BeTrue();
        await _cenario.PersistirAsync(processo);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo recarregado = await RecarregarAsync(contexto, processo.Id);
        AfirmarRegrasDaClassificacao(recarregado.Classificacao!);
        AfirmarParametrosDeEliminacao(recarregado.Classificacao!, etapaId);
        Guid regraId = configuracao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem).Id;
        await AfirmarDiscriminadorGravadoAsync(contexto, regraId);
    }

    [Fact(DisplayName = "Persiste e recarrega classificação CLASSIFICACAO-IMPORTADA sem arredondamento")]
    public async Task PersisteERecarrega_Importada_SemArredondamento()
    {
        ProcessoSeletivo processo = ProcessoComEtapa("PS 2026 — Transferência", TiposDeProcessoDeTeste.TransferenciaExterna);
        ConfiguracaoClassificacao classificacao = CriarClassificacao(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, "1"),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "2"),
        });
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();
        await _cenario.PersistirAsync(processo);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo recarregado = await RecarregarAsync(contexto, processo.Id);
        AfirmarClassificacaoImportada(recarregado.Classificacao!);
    }

    [Fact(DisplayName = "Reconfigurar classificação sobre o agregado rastreado insere os filhos novos, sem falhar na atualização")]
    public async Task ReconfigurarClassificacaoSobreAgregadoTracked_InsereFilhos()
    {
        ProcessoSeletivo processo = ProcessoComEtapa("PS 2026 — PSVR", TiposDeProcessoDeTeste.PSVR);
        processo.DefinirClassificacao(CriarClassificacao(ClassificacaoPonderada())).IsSuccess.Should().BeTrue();
        await _cenario.PersistirAsync(processo);
        await ReconfigurarClassificacaoAsync(processo.Id);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo recarregado = await RecarregarAsync(contexto, processo.Id);
        recarregado.Classificacao!.CasasArredondamento.Should().Be(4);
        recarregado.Classificacao.RegrasEliminacao.Should().ContainSingle();
    }

    [Fact(DisplayName = "Atualizar dados da mesma etapa, preservando o identificador, mantém a eliminação referenciando-a")]
    public async Task AtualizarEtapaMesmoId_MantemEliminacaoReferenciada()
    {
        ProcessoSeletivo processo = ProcessoComEtapa("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU);
        Guid etapaId = processo.Etapas.Single().Id;
        processo.DefinirClassificacao(ClassificacaoComNotaMinima(etapaId)).IsSuccess.Should().BeTrue();
        await _cenario.PersistirAsync(processo);
        await AtualizarEtapaAsync(processo.Id);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo recarregado = await RecarregarAsync(contexto, processo.Id);
        EtapaProcesso etapa = recarregado.Etapas.Single();
        etapa.Id.Should().Be(etapaId);
        etapa.Nome.Should().Be("Prova Objetiva (revisada)");
        recarregado.Classificacao!.RegrasEliminacao.Single().Parametros.Should().Be(new ParametrosElimNotaMinimaEtapa(etapaId, 3m));
    }

    [Theory(DisplayName = "O valor de BaseadoEmEnem persiste e volta do banco independentemente do tipo de processo")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PersisteERecarrega_BaseadoEmEnem(bool baseadoEmEnem)
    {
        ProcessoSeletivo processo = ProcessoComEtapa("PS 2026 — BaseadoEmEnem", TiposDeProcessoDeTeste.PSIQ);
        processo.DefinirClassificacao(CriarClassificacao(ClassificacaoPonderada(baseadoEmEnem))).IsSuccess.Should().BeTrue();
        await _cenario.PersistirAsync(processo);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo recarregado = await RecarregarAsync(contexto, processo.Id);
        recarregado.Classificacao!.BaseadoEmEnem.Should().Be(baseadoEmEnem,
            "o tipo de processo não decide o valor declarado");
    }

    private static ProcessoSeletivo ProcessoComEtapa(string nome, TipoProcessoSnapshot tipo, string? faseCodigo = null)
    {
        ProcessoSeletivo processo = NovoProcesso(nome, tipo);
        EtapaProcesso etapa = EtapaProcesso.Criar(IdentificacaoDaProva("Prova Objetiva", faseCodigo), AvaliacaoDaProva(1m)).Value!;
        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static IdentificacaoDaEtapa IdentificacaoDaProva(string nome, string? faseCodigo = null) =>
        new(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoDeEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: faseCodigo);

    private static DeclaracaoDaAvaliacaoDaEtapa AvaliacaoDaProva(decimal peso) =>
        new(Peso: peso, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null);

    private static DeclaracaoDaClassificacao ClassificacaoPonderada(bool baseadoEmEnem = false) => new()
    {
        RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, "a"),
        RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "b"),
        CasasArredondamento = 2,
        RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"),
        BaseadoEmEnem = baseadoEmEnem,
        ResolucaoPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
        QuadroPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : [],
        EscalaNotaFinal = 100m,
    };

    private static ConfiguracaoClassificacao CriarClassificacao(DeclaracaoDaClassificacao declaracao)
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(declaracao);
        resultado.IsSuccess.Should().BeTrue();
        return resultado.Value!;
    }

    private static ConfiguracaoClassificacao ClassificacaoComCincoVariantes(Guid etapaId) =>
        CriarClassificacao(ClassificacaoPonderada(baseadoEmEnem: true) with
        {
            RegrasEliminacao =
            [
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "a"), new ParametrosElimNotaMinimaEtapa(etapaId, 3m)).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimCorteEmArea, "b"), new ParametrosElimCorteEmArea("REDACAO", 400m)).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimZeroEmArea, "c"), new ParametrosElimZeroEmArea()).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "0"), new ParametrosElimFaltaEmDiaDeProvaEnem()).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, "7"), new ParametrosElimAusenciaNaEtapa(etapaId)).Value!,
            ],
        });

    private static ConfiguracaoClassificacao ClassificacaoComNotaMinima(Guid etapaId) =>
        CriarClassificacao(ClassificacaoPonderada() with
        {
            RegrasEliminacao = [RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "a"), new ParametrosElimNotaMinimaEtapa(etapaId, 3m)).Value!],
        });

    private async Task ReconfigurarClassificacaoAsync(Guid processoId)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repositorio = ProcessoSeletivoRepositoryDeTeste.Criar(contexto);
        ProcessoSeletivo processo = (await repositorio.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        ConfiguracaoClassificacao nova = CriarClassificacao(ClassificacaoPonderada(baseadoEmEnem: true) with
        {
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoArredondarCima, "e"),
            CasasArredondamento = 4,
            RegrasEliminacao = [RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimCorteEmArea, "d"), new ParametrosElimCorteEmArea("REDACAO", 350m)).Value!],
        });
        processo.DefinirClassificacao(nova).IsSuccess.Should().BeTrue();
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    // A atualização usa a instância rastreada para que a regra continue apontando a mesma etapa.
    private async Task AtualizarEtapaAsync(Guid processoId)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repositorio = ProcessoSeletivoRepositoryDeTeste.Criar(contexto);
        ProcessoSeletivo processo = (await repositorio.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.AtualizarDados(IdentificacaoDaProva("Prova Objetiva (revisada)"), AvaliacaoDaProva(2m));
        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private static Task<ProcessoSeletivo> RecarregarAsync(UniPlusDbContext contexto, Guid processoId) =>
        contexto.ProcessosSeletivos.Include(p => p.Etapas).Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .SingleAsync(p => p.Id == processoId, CancellationToken.None);

    private static void AfirmarRegrasDaClassificacao(ConfiguracaoClassificacao classificacao)
    {
        classificacao.RegraCalculo.Codigo.Should().Be(RegraCalculoCodigo.FormulaMediaPonderada);
        classificacao.RegraArredondamento!.Codigo.Should().Be(RegraArredondamentoCodigo.PrecisaoTruncar);
        classificacao.CasasArredondamento.Should().Be(2);
        classificacao.RegraOrdemAlocacao.Codigo.Should().Be(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria);
        classificacao.RegrasEliminacao.Should().HaveCount(5);
    }

    private static void AfirmarParametrosDeEliminacao(ConfiguracaoClassificacao classificacao, Guid etapaId)
    {
        ParametrosElimNotaMinimaEtapa nota = (ParametrosElimNotaMinimaEtapa)classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimNotaMinimaEtapa).Parametros;
        nota.EtapaRef.Should().Be(etapaId);
        nota.NotaMinima.Should().Be(3m);
        ParametrosElimCorteEmArea redacao = (ParametrosElimCorteEmArea)classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimCorteEmArea).Parametros;
        redacao.Minimo.Should().Be(400m);
        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimZeroEmArea).Parametros.Should().BeOfType<ParametrosElimZeroEmArea>();
        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem).Parametros.Should().BeOfType<ParametrosElimFaltaEmDiaDeProvaEnem>();
        classificacao.RegrasEliminacao.Single(r => r.Regra.Codigo == RegraEliminacaoCodigo.ElimAusenciaNaEtapa).Parametros.Should().Be(new ParametrosElimAusenciaNaEtapa(etapaId));
    }

    private static void AfirmarClassificacaoImportada(ConfiguracaoClassificacao classificacao)
    {
        classificacao.RegraCalculo.Codigo.Should().Be(RegraCalculoCodigo.ClassificacaoImportada);
        classificacao.RegraArredondamento.Should().BeNull();
        classificacao.CasasArredondamento.Should().BeNull();
        classificacao.RegrasEliminacao.Should().BeEmpty();
    }

    private static async Task AfirmarDiscriminadorGravadoAsync(UniPlusDbContext contexto, Guid regraId)
    {
        string parametrosGravados = await contexto.Database.SqlQuery<string>($"""
            SELECT args::text AS "Value" FROM selecao.regras_eliminacao WHERE id = {regraId}
            """).SingleAsync();
        parametrosGravados.Should().Contain("\"$tipo\":\"faltaEmDiaDeProvaEnem\"",
            "o discriminador gravado é o que a leitura usa para voltar à variante certa");
    }
}
