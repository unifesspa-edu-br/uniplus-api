namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

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

        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        Result etapasResult = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1),
            EtapaProcesso.Criar("Redação", CaraterEtapa.Ambas, peso: 2m, notaMinima: 5m, ordem: 2),
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
        recarregado.Etapas.Should().HaveCount(2);
        recarregado.CalcularDivisorMedia().Should().Be(5m);

        recarregado.OfertaAtendimento.Should().NotBeNull();
        recarregado.OfertaAtendimento!.Condicoes.Single().CondicaoOrigemId.Should().Be(condicaoOrigemId);
        recarregado.OfertaAtendimento.Recursos.Single().RecursoOrigemId.Should().Be(recursoOrigemId);
        recarregado.OfertaAtendimento.TiposDeficiencia.Single().TipoDeficienciaOrigemId.Should().Be(tipoDeficienciaOrigemId);
    }

    [Fact(DisplayName = "Reconfigurar etapas sobre o agregado carregado (tracked) insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarEtapasSobreAgregadoTracked_InsereFilhos()
    {
        // Reproduz o fluxo criar→configurar real (que o caminho AdicionarAsync não
        // cobria): carrega o agregado JÁ tracked via ObterComConfiguracaoAsync,
        // substitui a coleção de etapas por filhos com Guid v7 já preenchido e
        // salva. Sem a correção, DbSet.Update marcaria os filhos novos como
        // Modified e o SaveChanges emitiria UPDATE de linhas nunca inseridas.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSIQ", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)], PrecondicaoIfMatch.Ausente);

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
                EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1),
                EtapaProcesso.Criar("Entrevista", CaraterEtapa.Ambas, peso: 2m, ordem: 2),
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Derivação", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
        [
            RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
            RegraDerivacaoConfigurada.Criar(1, "AC_PCD",
                [CondicaoRegraDerivacao.Criar(1, "CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!,
        ]).Value!;
        processo.DefinirRegrasDerivacao([config]).IsSuccess.Should().BeTrue();

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Redefinição", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        processo.DefinirRegrasDerivacao(
        [
            ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
            [
                RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
                RegraDerivacaoConfigurada.Criar(1, "AC_PCD",
                    [CondicaoRegraDerivacao.Criar(1, "CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!,
            ]).Value!,
        ]).IsSuccess.Should().BeTrue();

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
            ]).IsSuccess.Should().BeTrue();

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
}
