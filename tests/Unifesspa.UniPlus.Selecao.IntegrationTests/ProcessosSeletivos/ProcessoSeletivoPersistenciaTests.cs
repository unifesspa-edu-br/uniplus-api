namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaPersistenciaDoProcessoSeletivo;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do agregado
/// <c>ProcessoSeletivo</c> na fatia F0 (fundação): persiste e recarrega a raiz
/// com etapas pontuadas e o atendimento especializado (3 níveis),
/// validando o mapeamento EF (HasMany/HasOne + FK, sem owned types) e a
/// migration aplicando limpo contra Postgres real.
/// </summary>
public sealed class ProcessoSeletivoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDoProcessoSeletivo _apoio;

    public ProcessoSeletivoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDoProcessoSeletivo(fixture);
    }

    [Fact(DisplayName = "Persiste e recarrega o agregado com etapas e atendimento especializado")]
    public async Task PersisteERecarrega_Fundacao()
    {
        Guid condicaoOrigemId = Guid.CreateVersion7();
        Guid recursoOrigemId = Guid.CreateVersion7();
        Guid tipoDeficienciaOrigemId = Guid.CreateVersion7();
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — SiSU");
        processo.DefinirEtapas([ProvaObjetivaComEscalaCompleta(), NovaEtapa("Redação", CaraterEtapa.Ambas, 2, DeclaracaoComPeso(2m, notaMinima: 5m))]).IsSuccess.Should().BeTrue();
        processo.DefinirAtendimentoEspecializado(AtendimentoDaFundacao(condicaoOrigemId, recursoOrigemId, tipoDeficienciaOrigemId)).IsSuccess.Should().BeTrue();
        await _apoio.GravarAsync(processo);

        ProcessoSeletivo recarregado = await RecarregarComEtapasEAtendimentoAsync(processo.Id);

        ConferirOProcessoEAsEtapasDaFundacao(recarregado);
        ConferirOAtendimentoDaFundacao(recarregado, condicaoOrigemId, recursoOrigemId, tipoDeficienciaOrigemId);
    }

    [Fact(DisplayName = "Persiste dois processos do mesmo tipo no mesmo DbContext sem compartilhar o owned snapshot")]
    public async Task Persistir_DoisProcessosDoMesmoTipoNoMesmoContexto_PreservaSnapshotEmAmbos()
    {
        ProcessoSeletivo[] recarregados = await GravarERecarregarDoisProcessosAsync();

        recarregados.Should().HaveCount(2);
        recarregados.Should().OnlyContain(processo =>
            processo.TipoProcesso.OrigemId == TiposDeProcessoDeTeste.Sisu.OrigemId
            && processo.TipoProcesso.Codigo == "SISU"
            && processo.TipoProcesso.Nome == "SiSU");
    }

    [Fact(DisplayName = "Persiste e recarrega a Unidade administradora sem perda, inclusive a cidade (CA-05)")]
    public async Task PersisteERecarrega_UnidadeAdministradora()
    {
        Guid unidadeId = Guid.NewGuid();
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — SiSU", unidadeOrigemId: unidadeId, unidade: UnidadeDoCeps(new CidadeCopiadaDaUnidade("1504208", "Marabá", "PA")));
        await _apoio.GravarAsync(processo);

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.UnidadeAdministradoraOrigemId.Should().Be(unidadeId);
        ConferirAUnidadeDoCepsEmMaraba(recarregado.UnidadeAdministradora);
    }

    [Fact(DisplayName = "CHECK ck_processos_seletivos_unidade_administradora_cidade_completa rejeita UPDATE cru com trio de cidade parcial")]
    public async Task CheckCidadeCompleta_RejeitaTrioParcial()
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — Cidade Parcial", unidade: UnidadeDoCeps(new CidadeCopiadaDaUnidade("1504208", "Marabá", "PA")));
        await _apoio.GravarAsync(processo);

        // Zera só o nome da cidade, deixando código/UF — trio parcial. O domínio trata a
        // cidade como all-or-nothing; o CHECK protege a escrita crua.
        await using UniPlusDbContext rawContext = _fixture.CreateDbContext();
        Func<Task> act = async () => await rawContext.Database.ExecuteSqlAsync(
            $"UPDATE selecao.processos_seletivos SET unidade_administradora_cidade_nome = NULL WHERE id = {processo.Id}");

        await act.Should().ThrowAsync<PostgresException>(
            "o CHECK ck_processos_seletivos_unidade_administradora_cidade_completa exige o trio de cidade completo ou ausente");
    }

    [Theory(DisplayName = "Situações válidas são gravadas e lidas do banco")]
    [InlineData("RASCUNHO", SituacaoDoProcessoSeletivo.Rascunho)]
    [InlineData("PUBLICADO", SituacaoDoProcessoSeletivo.Publicado)]
    [InlineData("ENCERRADO", SituacaoDoProcessoSeletivo.Encerrado)]
    [InlineData("CANCELADO", SituacaoDoProcessoSeletivo.Cancelado)]
    public async Task Situacao_CodigoValido_GravaELeDoBanco(string codigo, SituacaoDoProcessoSeletivo esperado)
    {
        ProcessoSeletivo processo = NovoProcesso("PS — Situação");
        await _apoio.GravarAsync(processo);
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        await contexto.Database.ExecuteSqlAsync($"UPDATE selecao.processos_seletivos SET situacao = {codigo} WHERE id = {processo.Id}");

        ProcessoSeletivo recarregado = await contexto.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == processo.Id);
        recarregado.Situacao.Should().Be(esperado);
    }

    [Fact(DisplayName = "CHECK de situação rejeita código inválido")]
    public async Task Situacao_CodigoInvalido_ViolaRestricao()
    {
        ProcessoSeletivo processo = NovoProcesso("PS — Situação inválida");
        await _apoio.GravarAsync(processo);
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        Func<Task> act = () => contexto.Database.ExecuteSqlAsync($"UPDATE selecao.processos_seletivos SET situacao = {"INVALIDO"} WHERE id = {processo.Id}");

        (await act.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_processos_seletivos_situacao");
    }

    [Theory(DisplayName = "CHECKs de positividade da etapa rejeitam UPDATE cru com nota máxima ou passo zerados")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChecksDaEscalaDaEtapa_RejeitamValorNaoPositivo(bool notaMaxima)
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — Escala");
        processo.DefinirEtapas([NovaEtapa("Prova Objetiva", CaraterEtapa.Classificatoria, 1, DeclaracaoComPeso(1m, notaMaxima: 10m, passo: 0.5m))]).IsSuccess.Should().BeTrue();
        await _apoio.GravarAsync(processo);

        Func<Task> act = () => ZerarANotaMaximaOuOPassoAsync(notaMaxima, processo.Id);

        await act.Should().ThrowAsync<PostgresException>(
            "numeric(18,4) arredonda em silêncio, e o EF hidrata sem passar pelo domínio");
    }

    [Fact(DisplayName = "Inserir sem Unidade administradora viola a constraint NOT NULL da coluna (CA-05)")]
    public async Task Inserir_SemUnidadeAdministradora_ViolaNotNull()
    {
        Guid processoId = Guid.CreateVersion7();

        await using UniPlusDbContext writeContext = _fixture.CreateDbContext();

        // A coluna é passada EXPLICITAMENTE como NULL, não omitida, e as demais colunas obrigatórias
        // recebem valor: o banco não tem valor padrão para nenhuma, então o INSERT só prova a constraint
        // NOT NULL da sigla se não falhar antes em outra coluna.
        Func<Task> act = () => InserirSemUnidadeAsync(writeContext, processoId);

        (await act.Should().ThrowAsync<PostgresException>(
            "unidade_administradora_sigla é NOT NULL, sem estratégia em duas fases"))
            .Which.ColumnName.Should().Be("unidade_administradora_sigla");
    }

    /// <summary>
    /// Reproduz o fluxo criar→configurar real (que o caminho AdicionarAsync não cobria): carrega o
    /// agregado JÁ tracked via ObterComConfiguracaoAsync, substitui a coleção de etapas por filhos com
    /// Guid v7 já preenchido e salva. Sem a correção, DbSet.Update marcaria os filhos novos como Modified
    /// e o SaveChanges emitiria UPDATE de linhas nunca inseridas.
    /// </summary>
    [Fact(DisplayName = "Reconfigurar etapas sobre o agregado carregado (tracked) insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarEtapasSobreAgregadoTracked_InsereFilhos()
    {
        ProcessoSeletivo processo = NovoProcesso("PS 2026 — PSIQ", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        processo.DefinirEtapas([NovaEtapa("Prova Objetiva", CaraterEtapa.Classificatoria, 1, DeclaracaoComPeso(1m))]);
        await _apoio.GravarAsync(processo);

        await SubstituirAsEtapasPorProvaEEntrevistaAsync(processo.Id);

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo recarregado = await readContext.ProcessosSeletivos.Include(p => p.Etapas).SingleAsync(p => p.Id == processo.Id, CancellationToken.None);
        recarregado.Etapas.Should().HaveCount(2);
        recarregado.Etapas.Select(e => e.Nome).Should().BeEquivalentTo(["Prova Objetiva", "Entrevista"]);
        recarregado.Declaracao().Avaliacao.CalcularDivisorMedia().Should().Be(5m);
    }

    [Fact(DisplayName = "Persiste e lê do banco a regra de derivação nos três níveis")]
    public async Task PersisteELeDoBanco_RegraDerivacao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Derivação", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        ConfiguracaoDerivacaoFato config = ConfiguracaoDaDerivacao();
        processo.DefinirRegrasDerivacao([config]).IsSuccess.Should().BeTrue();

        await _apoio.GravarAsync(processo);
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo? recarregado = await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConferirDerivacao(recarregado!);
    }

    private static void ConferirDerivacao(ProcessoSeletivo processo)
    {
        ConfiguracaoDerivacaoFato configuracaoRecarregada = processo.RegrasDerivacao.Single();
        configuracaoRecarregada.CodigoFato.Should().Be("REGIME_DE_VAGA");
        configuracaoRecarregada.Regras.Should().HaveCount(2, "os três níveis reidratam — a configuração, as regras e as condições");
        configuracaoRecarregada.Regras.Single(r => r.Contribui == "AC_PCD").Condicoes.Should().ContainSingle(c => c.Fato == "CONCORRER_PCD");

        // A avaliação usa a regra reconstruída com os dados cadastrados.
        DerivacaoDoFato derivacao = configuracaoRecarregada.ParaDerivacaoDoFato(["AC", "AC_PCD"]).Value!;
        derivacao.Regras.Should().HaveCount(2);
        derivacao.DependenciasDeclaradas.Should().BeEquivalentTo(["CONCORRER_PCD"]);
    }

    private static ConfiguracaoDerivacaoFato ConfiguracaoDaDerivacao()
    {
        return ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA",
        [
            RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
            RegraDerivacaoConfigurada.Criar(1, "AC_PCD",
                [CondicaoRegraDerivacao.Criar(1, "CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!,
        ]).Value!;
    }

    private static Task<int> InserirSemUnidadeAsync(UniPlusDbContext writeContext, Guid processoId) =>
        writeContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO selecao.processos_seletivos (
                id, nome, tipo_processo_origem_id, tipo_processo_codigo, tipo_processo_nome,
                situacao, origem_candidatos, localidade_codigo_ibge, localidade_nome, localidade_uf,
                unidade_administradora_origem_id,
                unidade_administradora_sigla, unidade_administradora_identificador_no_endereco_web, unidade_administradora_nome, unidade_administradora_tipo,
                created_at, is_deleted)
            VALUES (
                {processoId}, 'PS sem unidade', {TiposDeProcessoDeTeste.Sisu.OrigemId}, 'SISU', 'SiSU', 'RASCUNHO', 'INSCRICAO_PROPRIA', '1504208', 'Marabá', 'PA',
                {Guid.NewGuid()},
                NULL, 'ceps', 'Centro de Processos Seletivos', 'ADMINISTRATIVA',
                now(), false)
            """,
            CancellationToken.None);

    private async Task<ProcessoSeletivo[]> GravarERecarregarDoisProcessosAsync()
    {
        ProcessoSeletivo processoA = NovoProcesso("PS A");
        ProcessoSeletivo processoB = NovoProcesso("PS B");

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            await repository.AdicionarAsync(processoA, CancellationToken.None);
            await repository.AdicionarAsync(processoB, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        return await readContext.ProcessosSeletivos.AsNoTracking()
            .Where(processo => processo.Id == processoA.Id || processo.Id == processoB.Id).ToArrayAsync(CancellationToken.None);
    }

    private static ProcessoSeletivo NovoProcesso(
        string nome, TipoProcessoSnapshot? tipo = null, UnidadeAdministradoraSnapshot? unidade = null, Guid? unidadeOrigemId = null) =>
        ProcessoSeletivo.Criar(
            nome, tipo ?? TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, unidadeOrigemId ?? Guid.NewGuid(),
            unidade ?? UnidadeDoCeps(), LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static UnidadeAdministradoraSnapshot UnidadeDoCeps(CidadeCopiadaDaUnidade? cidade = null) =>
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA", cidade).Value!;

    private static EtapaProcesso NovaEtapa(string nome, CaraterEtapa carater, int ordem, DeclaracaoDaAvaliacaoDaEtapa declaracao) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves.FormacaoDeChaveLocal.Formar("etapa", nome, new()),
            new IdentificacaoDaEtapa(
                Nome: nome, Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                Ordem: ordem, FaseCodigo: null),
            declaracao).Value!;

    private static DeclaracaoDaAvaliacaoDaEtapa DeclaracaoComPeso(decimal peso, decimal? notaMinima = null, decimal? notaMaxima = null, decimal? passo = null) =>
        new(Peso: peso, NotaMinima: notaMinima, NotaMaxima: notaMaxima, PassoDoLancamento: passo, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null);

    private static EtapaProcesso ProvaObjetivaComEscalaCompleta() =>
        NovaEtapa(
            "Prova Objetiva", CaraterEtapa.Classificatoria, 1,
            DeclaracaoComPeso(3m, notaMaxima: 10m, passo: 0.25m) with
            {
                FormaDeAvaliacao = FormaDeAvaliacao.Humana,
                RegraConsolidacao = ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), "v1", new string('c', 64)),
            });

    private static AtendimentoEspecializado AtendimentoDaFundacao(Guid condicaoOrigemId, Guid recursoOrigemId, Guid tipoDeficienciaOrigemId)
    {
        Result<AtendimentoEspecializado> atendimento = AtendimentoEspecializado.Criar(
            condicoes: [CondicaoOfertada.Criar(condicaoOrigemId, "PCD", "Pessoa com deficiência")],
            recursos: [RecursoOfertado.Criar(recursoOrigemId, "LEDOR", "Ledor")],
            tiposDeficiencia: [DeficienciaAtendida.Criar(tipoDeficienciaOrigemId, "DEFICIENCIA_VISUAL", "Deficiência visual")]);
        atendimento.IsSuccess.Should().BeTrue();

        return atendimento.Value!;
    }

    private async Task<ProcessoSeletivo> RecarregarComEtapasEAtendimentoAsync(Guid processoId)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.AtendimentoEspecializado!).ThenInclude(o => o.Condicoes)
            .Include(p => p.AtendimentoEspecializado!).ThenInclude(o => o.Recursos)
            .Include(p => p.AtendimentoEspecializado!).ThenInclude(o => o.TiposDeficiencia)
            .FirstOrDefaultAsync(p => p.Id == processoId, CancellationToken.None);

        recarregado.Should().NotBeNull();
        return recarregado!;
    }

    private static void ConferirOProcessoEAsEtapasDaFundacao(ProcessoSeletivo recarregado)
    {
        recarregado.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Rascunho);
        recarregado.TipoProcesso.OrigemId.Should().Be(TiposDeProcessoDeTeste.Sisu.OrigemId);
        recarregado.TipoProcessoOrigemId.Should().Be(TiposDeProcessoDeTeste.Sisu.OrigemId);
        recarregado.TipoProcesso.Codigo.Should().Be("SISU");
        recarregado.Etapas.Should().HaveCount(2);
        recarregado.Declaracao().Avaliacao.CalcularDivisorMedia().Should().Be(5m);
        recarregado.Etapas.Single(e => e.Ordem == 1).Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == 10m && e.PassoDoLancamento == 0.25m
                && e.FormaDeAvaliacao == FormaDeAvaliacao.Humana
                && e.RegraConsolidacao!.Codigo == RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo());
        recarregado.Etapas.Single(e => e.Ordem == 2).Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == null && e.PassoDoLancamento == null
                && e.FormaDeAvaliacao == null && e.RegraConsolidacao == null);
    }

    private static void ConferirOAtendimentoDaFundacao(ProcessoSeletivo recarregado, Guid condicaoOrigemId, Guid recursoOrigemId, Guid tipoDeficienciaOrigemId)
    {
        recarregado.AtendimentoEspecializado.Should().NotBeNull();
        recarregado.AtendimentoEspecializado!.Condicoes.Single().CondicaoOrigemId.Should().Be(condicaoOrigemId);
        recarregado.AtendimentoEspecializado.Recursos.Single().RecursoOrigemId.Should().Be(recursoOrigemId);
        recarregado.AtendimentoEspecializado.Recursos.Single().RecursoCodigo.Should().Be("LEDOR");
        recarregado.AtendimentoEspecializado.TiposDeficiencia.Single().TipoDeficienciaOrigemId.Should().Be(tipoDeficienciaOrigemId);
    }

    private static void ConferirAUnidadeDoCepsEmMaraba(UnidadeAdministradoraSnapshot unidade)
    {
        unidade.Sigla.Should().Be("CEPS");
        unidade.IdentificadorNoEnderecoWeb.Should().Be("ceps");
        unidade.Nome.Should().Be("Centro de Processos Seletivos");
        unidade.Tipo.Should().Be("ADMINISTRATIVA");
        unidade.CidadeCodigoIbge.Should().Be("1504208");
        unidade.CidadeNome.Should().Be("Marabá");
        unidade.CidadeUf.Should().Be("PA");
    }

    private async Task<int> ZerarANotaMaximaOuOPassoAsync(bool notaMaxima, Guid processoId)
    {
        await using UniPlusDbContext rawContext = _fixture.CreateDbContext();
        return notaMaxima
            ? await rawContext.Database.ExecuteSqlAsync($"UPDATE selecao.etapas_processo SET nota_maxima = 0 WHERE processo_seletivo_id = {processoId}")
            : await rawContext.Database.ExecuteSqlAsync($"UPDATE selecao.etapas_processo SET passo_do_lancamento = 0 WHERE processo_seletivo_id = {processoId}");
    }

    /// <summary>Persistência por change detection sobre o agregado tracked.</summary>
    private async Task SubstituirAsEtapasPorProvaEEntrevistaAsync(Guid processoId)
    {
        await using UniPlusDbContext configureContext = _fixture.CreateDbContext();
        ProcessoSeletivo carregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(configureContext).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        carregado.DefinirEtapas(
        [
            NovaEtapa("Prova Objetiva", CaraterEtapa.Classificatoria, 1, DeclaracaoComPeso(3m)),
            NovaEtapa("Entrevista", CaraterEtapa.Ambas, 2, DeclaracaoComPeso(2m)),
        ]).IsSuccess.Should().BeTrue();

        await configureContext.SaveChangesAsync(CancellationToken.None);
    }
}
