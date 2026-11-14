namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
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
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do agregado
/// <c>ProcessoSeletivo</c> na fatia F0 (fundação): persiste e recarrega a raiz
/// com etapas pontuadas e a oferta de atendimento especializado (3 níveis),
/// validando o mapeamento EF (HasMany/HasOne + FK, sem owned types) e a
/// migration aplicando limpo contra Postgres real.
/// </summary>
public sealed partial class ProcessoSeletivoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
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

        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        Result etapasResult = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 3m, ordem: 1, notaMaxima: 10m, passoDoLancamento: 0.25m, formaDeAvaliacao: FormaDeAvaliacao.Humana,
                regraConsolidacao: ReferenciaRegra.Criar(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('c', 64)).Value!).Value!,
            EtapaProcesso.Criar("Redação", CaraterEtapa.Ambas, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 2m, notaMinima: 5m, ordem: 2).Value!,
        ], PrecondicaoIfMatch.Ausente);
        etapasResult.IsSuccess.Should().BeTrue();

        Result<OfertaAtendimentoEspecializado> ofertaResult = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(condicaoOrigemId, "PCD", "Pessoa com deficiência")],
            recursos: [OfertaRecurso.Criar(recursoOrigemId, "LEDOR", "Ledor")],
            tiposDeficiencia: [OfertaTipoDeficiencia.Criar(tipoDeficienciaOrigemId, "DEFICIENCIA_VISUAL", "Deficiência visual")]);
        ofertaResult.IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(ofertaResult.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
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
        EstadoDaConfiguracao.De(recarregado).CalcularDivisorMedia().Should().Be(5m);
        recarregado.Etapas.Single(e => e.Ordem == 1).Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == 10m && e.PassoDoLancamento == 0.25m
                && e.FormaDeAvaliacao == FormaDeAvaliacao.Humana
                && e.RegraConsolidacao!.Codigo == RegraConsolidacaoCodigo.AvaliacaoUnica);
        recarregado.Etapas.Single(e => e.Ordem == 2).Should().Match<EtapaProcesso>(
            e => e.NotaMaxima == null && e.PassoDoLancamento == null
                && e.FormaDeAvaliacao == null && e.RegraConsolidacao == null);

        recarregado.OfertaAtendimento.Should().NotBeNull();
        recarregado.OfertaAtendimento!.Condicoes.Single().CondicaoOrigemId.Should().Be(condicaoOrigemId);
        recarregado.OfertaAtendimento.Recursos.Single().RecursoOrigemId.Should().Be(recursoOrigemId);
        recarregado.OfertaAtendimento.Recursos.Single().RecursoCodigo.Should().Be("LEDOR");
        recarregado.OfertaAtendimento.TiposDeficiencia.Single().TipoDeficienciaOrigemId.Should().Be(tipoDeficienciaOrigemId);
    }

    [Fact(DisplayName = "Persiste dois processos do mesmo tipo no mesmo DbContext sem compartilhar o owned snapshot")]
    public async Task Persistir_DoisProcessosDoMesmoTipoNoMesmoContexto_PreservaSnapshotEmAmbos()
    {
        ProcessoSeletivo processoA = ProcessoSeletivo.Criar(
            "PS snapshot A", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        ProcessoSeletivo processoB = ProcessoSeletivo.Criar(
            "PS snapshot B", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processoA, CancellationToken.None);
            await repository.AdicionarAsync(processoB, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
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
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
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
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Zera só o nome da cidade, deixando código/UF — trio parcial. O domínio trata a
        // cidade como all-or-nothing; o CHECK protege a escrita crua.
        await using UniPlusDbContext rawContext = _fixture.CreateDbContext();
        Func<Task> act = async () => await rawContext.Database.ExecuteSqlAsync(
            $"UPDATE selecao.processos_seletivos SET unidade_administradora_cidade_nome = NULL WHERE id = {processo.Id}");

        await act.Should().ThrowAsync<PostgresException>(
            "o CHECK ck_processos_seletivos_unidade_administradora_cidade_completa exige o trio de cidade completo ou ausente");
    }

    [Theory(DisplayName = "CHECKs de positividade da etapa rejeitam UPDATE cru com nota máxima ou passo zerados")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChecksDaEscalaDaEtapa_RejeitamValorNaoPositivo(bool notaMaxima)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Escala", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1, notaMaxima: 10m, passoDoLancamento: 0.5m).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext rawContext = _fixture.CreateDbContext();
        Func<Task> act = async () => _ = notaMaxima
            ? await rawContext.Database.ExecuteSqlAsync(
                $"UPDATE selecao.etapas_processo SET nota_maxima = 0 WHERE processo_seletivo_id = {processo.Id}")
            : await rawContext.Database.ExecuteSqlAsync(
                $"UPDATE selecao.etapas_processo SET passo_do_lancamento = 0 WHERE processo_seletivo_id = {processo.Id}");

        await act.Should().ThrowAsync<PostgresException>(
            "numeric(18,4) arredonda em silêncio, e o EF hidrata sem passar pelo domínio");
    }

    [Fact(DisplayName = "Inserir sem Unidade administradora viola a constraint NOT NULL da coluna (issue #849, CA-05)")]
    public async Task Inserir_SemUnidadeAdministradora_ViolaNotNull()
    {
        Guid processoId = Guid.CreateVersion7();

        await using UniPlusDbContext writeContext = _fixture.CreateDbContext();

        // A coluna é passada EXPLICITAMENTE como NULL, não omitida, e as demais colunas obrigatórias
        // recebem valor: o banco não tem valor padrão para nenhuma, então o INSERT só prova a constraint
        // NOT NULL da sigla se não falhar antes em outra coluna.
        Func<Task> act = async () => await writeContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO selecao.processos_seletivos (
                id, nome, tipo_processo_origem_id, tipo_processo_codigo, tipo_processo_nome,
                status, origem_candidatos, localidade_codigo_ibge, localidade_nome, localidade_uf,
                unidade_administradora_origem_id,
                unidade_administradora_sigla, unidade_administradora_slug, unidade_administradora_nome, unidade_administradora_tipo,
                created_at, is_deleted)
            VALUES (
                {processoId}, 'PS sem unidade', {TipoProcesso.SiSU.OrigemId}, 'SiSU', 'SiSU', 1, 1, '1504208', 'Marabá', 'PA',
                {Guid.NewGuid()},
                NULL, 'ceps', 'Centro de Processos Seletivos', 'ADMINISTRATIVA',
                now(), false)
            """,
            CancellationToken.None);

        (await act.Should().ThrowAsync<PostgresException>(
            "unidade_administradora_sigla é NOT NULL, sem estratégia em duas fases"))
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSIQ", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1).Value!], PrecondicaoIfMatch.Ausente);

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (UniPlusDbContext configureContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(configureContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

            Result result = carregado.DefinirEtapas(
            [
                EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 3m, ordem: 1).Value!,
                EtapaProcesso.Criar("Entrevista", CaraterEtapa.Ambas, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 2m, ordem: 2).Value!,
            ], PrecondicaoIfMatch.Ausente);
            result.IsSuccess.Should().BeTrue();

            // Persistência por change detection sobre o agregado tracked.
            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Etapas)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.Etapas.Should().HaveCount(2);
        recarregado.Etapas.Select(e => e.Nome).Should().BeEquivalentTo(["Prova Objetiva", "Entrevista"]);
        EstadoDaConfiguracao.De(recarregado).CalcularDivisorMedia().Should().Be(5m);
    }

    [Fact(DisplayName = "Persiste e reidrata a regra de derivação nos três níveis, reconstruindo o value object do motor")]
    public async Task PersisteEReidrata_RegraDerivacao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Derivação", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA",
        [
            RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
            RegraDerivacaoConfigurada.Criar(1, "AC_PCD",
                [CondicaoRegraDerivacao.Criar(1, "CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!,
        ]).Value!;
        processo.DefinirRegrasDerivacao([config], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository readRepo = new(readContext, TimeProvider.System);
        ProcessoSeletivo? recarregado = await readRepo.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        ConfiguracaoDerivacaoFato recarregadaConfig = recarregado!.RegrasDerivacao.Single();
        recarregadaConfig.CodigoFato.Should().Be("REGIME_DE_VAGA");
        recarregadaConfig.Regras.Should().HaveCount(2, "os três níveis reidratam — a configuração, as regras e as condições");
        recarregadaConfig.Regras.Single(r => r.Contribui == "AC_PCD").Condicoes.Should().ContainSingle(c => c.Fato == "CONCORRER_PCD");

        // Round-trip completo: a reconstrução do value object que o motor consome bate com o cadastro.
        DerivacaoDoFato vo = recarregadaConfig.ParaDerivacaoDoFato(["AC", "AC_PCD"]).Value!;
        vo.Regras.Should().HaveCount(2);
        vo.DependenciasDeclaradas.Should().BeEquivalentTo(["CONCORRER_PCD"]);
    }
}
