namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do agregado
/// <c>ProcessoSeletivo</c> na fatia F0 (fundação): persiste e recarrega a raiz
/// com etapas pontuadas e a oferta de atendimento especializado (3 níveis),
/// validando o mapeamento EF (HasMany/HasOne + FK, sem owned types) e a
/// migration aplicando limpo contra Postgres real.
/// </summary>
public sealed class ProcessoSeletivoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public ProcessoSeletivoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Persiste e recarrega o agregado com etapas e atendimento especializado")]
    public async Task PersisteERecarrega_Fundacao()
    {
        Guid condicaoOrigemId = Guid.CreateVersion7();
        Guid recursoOrigemId = Guid.CreateVersion7();
        Guid tipoDeficienciaOrigemId = Guid.CreateVersion7();

        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        Result etapasResult = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 3m, ordem: 1).Value!,
            EtapaProcesso.Criar("Redação", CaraterEtapa.Ambas, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 2m, notaMinima: 5m, ordem: 2).Value!,
        ], PrecondicaoIfMatch.Ausente);
        etapasResult.IsSuccess.Should().BeTrue();

        Result<OfertaAtendimentoEspecializado> ofertaResult = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(condicaoOrigemId, "PCD", "Pessoa com deficiência")],
            recursos: [OfertaRecurso.Criar(recursoOrigemId, "Ledor")],
            tiposDeficiencia: [OfertaTipoDeficiencia.Criar(tipoDeficienciaOrigemId, "Deficiência visual")]);
        ofertaResult.IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(ofertaResult.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Condicoes)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.TiposDeficiencia)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.Status.Should().Be(StatusProcesso.Rascunho);
        recarregado.TipoProcesso.OrigemId.Should().Be(TipoProcesso.SiSU.OrigemId);
        recarregado.TipoProcessoOrigemId.Should().Be(TipoProcesso.SiSU.OrigemId);
        recarregado.TipoProcesso.Codigo.Should().Be("SiSU");
        recarregado.Etapas.Should().HaveCount(2);
        recarregado.CalcularDivisorMedia().Should().Be(5m);

        recarregado.OfertaAtendimento.Should().NotBeNull();
        recarregado.OfertaAtendimento!.Condicoes.Single().CondicaoOrigemId.Should().Be(condicaoOrigemId);
        recarregado.OfertaAtendimento.Recursos.Single().RecursoOrigemId.Should().Be(recursoOrigemId);
        recarregado.OfertaAtendimento.TiposDeficiencia.Single().TipoDeficienciaOrigemId.Should().Be(tipoDeficienciaOrigemId);
    }

    [Fact(DisplayName = "Persiste dois processos do mesmo tipo no mesmo DbContext sem compartilhar o owned snapshot")]
    public async Task Persistir_DoisProcessosDoMesmoTipoNoMesmoContexto_PreservaSnapshotEmAmbos()
    {
        ProcessoSeletivo processoA = ProcessoSeletivo.Criar(
            "PS snapshot A", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        ProcessoSeletivo processoB = ProcessoSeletivo.Criar(
            "PS snapshot B", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processoA, CancellationToken.None);
            await repository.AdicionarAsync(processoB, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo[] recarregados = await readContext.ProcessosSeletivos
            .AsNoTracking()
            .Where(processo => processo.Id == processoA.Id || processo.Id == processoB.Id)
            .ToArrayAsync(CancellationToken.None);

        recarregados.Should().HaveCount(2);
        recarregados.Should().OnlyContain(processo =>
            processo.TipoProcesso.OrigemId == TipoProcesso.SiSU.OrigemId
            && processo.TipoProcesso.Codigo == "SiSU"
            && processo.TipoProcesso.Nome == "SiSU");
    }

    [Fact(DisplayName = "Persiste e recarrega a Unidade administradora sem perda, inclusive a cidade (issue #849 CA-05, issue #1114)")]
    public async Task PersisteERecarrega_UnidadeAdministradora()
    {
        Guid unidadeId = Guid.NewGuid();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, unidadeId,
            UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA",
                cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "PA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.UnidadeAdministradoraOrigemId.Should().Be(unidadeId);
        recarregado.UnidadeAdministradora.Sigla.Should().Be("CEPS");
        recarregado.UnidadeAdministradora.Slug.Should().Be("ceps");
        recarregado.UnidadeAdministradora.Nome.Should().Be("Centro de Processos Seletivos");
        recarregado.UnidadeAdministradora.Tipo.Should().Be("ADMINISTRATIVA");
        recarregado.UnidadeAdministradora.CidadeCodigoIbge.Should().Be("1504208");
        recarregado.UnidadeAdministradora.CidadeNome.Should().Be("Marabá");
        recarregado.UnidadeAdministradora.CidadeUf.Should().Be("PA");
    }

    [Fact(DisplayName = "CHECK ck_processos_seletivos_unidade_administradora_cidade_completa rejeita UPDATE cru com trio de cidade parcial (issue #1114)")]
    public async Task CheckCidadeCompleta_RejeitaTrioParcial()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — Cidade Parcial", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar(
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA",
                cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "PA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Zera só o nome da cidade, deixando código/UF — trio parcial. O domínio trata a
        // cidade como all-or-nothing; o CHECK protege a escrita crua.
        await using SelecaoDbContext rawContext = _fixture.CreateDbContext();
        Func<Task> act = async () => await rawContext.Database.ExecuteSqlAsync(
            $"UPDATE selecao.processos_seletivos SET unidade_administradora_cidade_nome = NULL WHERE id = {processo.Id}");

        await act.Should().ThrowAsync<PostgresException>(
            "o CHECK ck_processos_seletivos_unidade_administradora_cidade_completa exige o trio de cidade completo ou ausente");
    }

    [Fact(DisplayName = "Inserir sem Unidade administradora viola a constraint NOT NULL da coluna (issue #849, CA-05)")]
    public async Task Inserir_SemUnidadeAdministradora_ViolaNotNull()
    {
        Guid processoId = Guid.CreateVersion7();

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();

        // A coluna é passada EXPLICITAMENTE como NULL, não omitida — `AddColumn(defaultValue: "")`
        // da migration inicial deixa um DEFAULT '' na coluna (formalidade do EF para o ALTER TABLE
        // em bases com linhas pré-existentes, que este projeto pré-produção não tem); omitir a
        // coluna do INSERT cairia nesse default em vez de provar a constraint NOT NULL.
        Func<Task> act = async () => await writeContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO selecao.processos_seletivos (
                id, nome, tipo_processo_origem_id, tipo_processo_codigo, tipo_processo_nome,
                status, origem_candidatos, unidade_administradora_origem_id,
                unidade_administradora_sigla, unidade_administradora_slug, unidade_administradora_nome, unidade_administradora_tipo,
                created_at, is_deleted)
            VALUES (
                {processoId}, 'PS sem unidade', {TipoProcesso.SiSU.OrigemId}, 'SiSU', 'SiSU', 1, 1, {Guid.NewGuid()},
                NULL, 'ceps', 'Centro de Processos Seletivos', 'ADMINISTRATIVA',
                now(), false)
            """,
            CancellationToken.None);

        (await act.Should().ThrowAsync<PostgresException>(
            "unidade_administradora_sigla é NOT NULL desde a migration inicial — sem produção, sem estratégia em duas fases"))
            .Which.ColumnName.Should().Be("unidade_administradora_sigla");
    }

    [Fact(DisplayName = "Reconfigurar etapas sobre o agregado carregado (tracked) insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarEtapasSobreAgregadoTracked_InsereFilhos()
    {
        // Reproduz o fluxo criar→configurar real (que o caminho AdicionarAsync não
        // cobria): carrega o agregado JÁ tracked via ObterComConfiguracaoAsync,
        // substitui a coleção de etapas por filhos com Guid v7 já preenchido e
        // salva. Sem a correção, DbSet.Update marcaria os filhos novos como
        // Modified e o SaveChanges emitiria UPDATE de linhas nunca inseridas.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSIQ", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Ausente);

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

            Result result = carregado.DefinirEtapas(
            [
                EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 3m, ordem: 1).Value!,
                EtapaProcesso.Criar("Entrevista", CaraterEtapa.Ambas, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 2m, ordem: 2).Value!,
            ], PrecondicaoIfMatch.Ausente);
            result.IsSuccess.Should().BeTrue();

            // Persistência por change detection sobre o agregado tracked.
            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Etapas)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.Etapas.Should().HaveCount(2);
        recarregado.Etapas.Select(e => e.Nome).Should().BeEquivalentTo(["Prova Objetiva", "Entrevista"]);
        recarregado.CalcularDivisorMedia().Should().Be(5m);
    }

    [Fact(DisplayName = "Persiste e reidrata a regra de derivação nos três níveis, reconstruindo o value object do motor")]
    public async Task PersisteEReidrata_RegraDerivacao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Derivação", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
        [
            RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
            RegraDerivacaoConfigurada.Criar(1, "AC_PCD",
                [CondicaoRegraDerivacao.Criar(1, "CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!,
        ]).Value!;
        processo.DefinirRegrasDerivacao([config], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository readRepo = new(readContext, TimeProvider.System);
        ProcessoSeletivo? recarregado = await readRepo.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoDerivacaoFato recarregadaConfig = recarregado!.RegrasDerivacao.Single();
        recarregadaConfig.CodigoFato.Should().Be("MODALIDADE");
        recarregadaConfig.Regras.Should().HaveCount(2, "os três níveis reidratam — a configuração, as regras e as condições");
        recarregadaConfig.Regras.Single(r => r.Contribui == "AC_PCD").Condicoes.Should().ContainSingle(c => c.Fato == "CONCORRER_PCD");

        // Round-trip completo: a reconstrução do value object que o motor consome bate com o cadastro.
        RegrasDerivacaoFato vo = recarregadaConfig.ParaRegrasDerivacao(RegrasDerivacaoModalidadeLei12711.DominioCanonico).Value!;
        vo.Regras.Should().HaveCount(2);
        vo.DependenciasDeclaradas.Should().BeEquivalentTo(["CONCORRER_PCD"]);
    }

    [Fact(DisplayName = "Redefinir a regra de derivação sobre o agregado carregado apaga a árvore antiga inteira (cascade órfão nos três níveis)")]
    public async Task RedefinirRegraDerivacao_SobreAgregadoTracked_ApagaArvoreAntiga()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Redefinição", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirRegrasDerivacao(
        [
            ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
            [
                RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
                RegraDerivacaoConfigurada.Criar(1, "AC_PCD",
                    [CondicaoRegraDerivacao.Criar(1, "CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!,
            ]).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Substitui a configuração por inteiro sobre o agregado JÁ tracked (fluxo real de reconfiguração):
        // Clear+Add deve marcar as regras e condições antigas como órfãs e o cascade deve deletá-las.
        await using (SelecaoDbContext mutateContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(mutateContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            carregado.DefinirRegrasDerivacao(
            [
                ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
                [
                    RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
                ]).Value!,
            ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            await mutateContext.SaveChangesAsync(CancellationToken.None);
        }

        // Contexto novo: a árvore antiga inteira sumiu — sobra só a única regra âncora sem condição.
        // Contagens escopadas a ESTE processo (a fixture é compartilhada entre testes da classe).
        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Guid configId = await readContext.Set<ConfiguracaoDerivacaoFato>()
            .Where(c => c.ProcessoSeletivoId == processo.Id).Select(c => c.Id).SingleAsync(CancellationToken.None);

        List<Guid> regraIds = await readContext.Set<RegraDerivacaoConfigurada>()
            .Where(r => r.ConfiguracaoDerivacaoFatoId == configId).Select(r => r.Id).ToListAsync(CancellationToken.None);
        regraIds.Should().HaveCount(1, "a regra AC_PCD antiga foi apagada como órfã");

        int condicoesRestantes = await readContext.Set<CondicaoRegraDerivacao>()
            .CountAsync(c => regraIds.Contains(c.RegraDerivacaoConfiguradaId), CancellationToken.None);
        condicoesRestantes.Should().Be(0, "a condição da regra AC_PCD antiga foi apagada em cascata");

        ProcessoSeletivoRepository readRepo = new(readContext, TimeProvider.System);
        ProcessoSeletivo recarregado = (await readRepo.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
        recarregado.RegrasDerivacao.Single().Regras.Should().ContainSingle(r => r.Contribui == "AC");
    }

    /// <summary>
    /// Prova mecânica (issue #850, §3.4/CA-05) de que <c>ObterComConfiguracaoAsync</c> emite
    /// <b>mais de um</b> <c>SELECT</c> para o grafo de <c>Include</c> — o comportamento que
    /// <c>.AsSplitQuery()</c> já produz em <c>ComConfiguracao</c> (adicionado na story do
    /// cronograma de fases, 15/07). Sem esta prova, remover o <c>.AsSplitQuery()</c> ou
    /// acrescentar uma nova coleção sem ele voltaria a arriscar o produto cartesiano de
    /// TODAS as coleções irmãs (etapas × modalidades × eliminação × fases × bancas…) num
    /// único <c>JOIN</c> — o defeito que a guarda de fitness test (ArchTests) impede em
    /// texto, e que este teste prova em runtime.
    /// </summary>
    [Fact(DisplayName = "ObterComConfiguracaoAsync com AsSplitQuery emite mais de um SELECT (#850, CA-05)")]
    public async Task ObterComConfiguracaoAsync_ComAsSplitQuery_EmiteMultiplosSelects()
    {
        Guid condicaoOrigemId = Guid.CreateVersion7();
        Guid recursoOrigemId = Guid.CreateVersion7();
        Guid modalidadeOrigemId = Guid.CreateVersion7();

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — AsSplitQuery", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        EtapaProcesso etapa = EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(condicaoOrigemId, "PCD", "Pessoa com deficiência")],
            recursos: [OfertaRecurso.Criar(recursoOrigemId, "Ledor")],
            tiposDeficiencia: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixoTeste).Value!;
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId, "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null,
            "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 40, pr: 1m, regraDistribuicao, regraAjuste: null,
            referenciaDemografica: null, [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", HashFixoTeste).Value!,
            new ArgsElimNotaMinimaEtapa(etapa.Id, 4m)).Value!;
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", HashFixoTeste).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", HashFixoTeste).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixoTeste).Value!,
            1, [eliminacao], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_PRELIMINAR",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria, agrupaEtapas: true,
            permiteComplementacao: false, produzResultado: true, resultadoDefinitivo: false, coletaInscricao: false,
            inicio: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_PRELIMINAR", atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [BancaRequerida.Criar(Guid.CreateVersion7(), "BANCA_ANALISE_DOCUMENTAL")],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        CapturadorDeSql capturador = new();
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System, userContext: null),
                new AuditableInterceptor(TimeProvider.System, userContext: null),
                capturador)
            .Options;

        await using SelecaoDbContext context = new(options);
        ProcessoSeletivoRepository leitura = new(context, TimeProvider.System);

        ProcessoSeletivo? recarregado = await leitura.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        capturador.Comandos.Where(c => c.Contains("SELECT", StringComparison.OrdinalIgnoreCase)).Should()
            .HaveCountGreaterThan(1,
                "AsSplitQuery() emite uma consulta própria por coleção incluída — um único SELECT indicaria que " +
                "a flag foi removida e o carregamento voltou a produzir o produto cartesiano num JOIN só");

        // Contagens materializadas batem com o fixture — cada navegação afirmada tem ao
        // menos 1 item (um fixture todo vazio não distingue "coleção carregada" de
        // "coleção omitida por engano").
        recarregado!.Etapas.Should().ContainSingle();
        recarregado.OfertaAtendimento!.Condicoes.Should().ContainSingle().Which.CondicaoOrigemId.Should().Be(condicaoOrigemId);
        recarregado.OfertaAtendimento.Recursos.Should().ContainSingle().Which.RecursoOrigemId.Should().Be(recursoOrigemId);
        recarregado.DistribuicaoVagas.Should().ContainSingle();
        recarregado.DistribuicaoVagas.Single().Modalidades.Should().ContainSingle()
            .Which.ModalidadeOrigemId.Should().Be(modalidadeOrigemId);
        recarregado.Classificacao!.RegrasEliminacao.Should().ContainSingle();
        recarregado.CronogramaFases.Should().ContainSingle();
        recarregado.CronogramaFases.Single().BancasRequeridas.Should().ContainSingle();
    }

    private const string HashFixoTeste = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Captura o SQL emitido — mesmo padrão de <c>VersaoVigentePersistenciaTests.CapturadorDeSql</c>.</summary>
    private sealed class CapturadorDeSql : DbCommandInterceptor
    {
        public List<string> Comandos { get; } = [];

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            Comandos.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
