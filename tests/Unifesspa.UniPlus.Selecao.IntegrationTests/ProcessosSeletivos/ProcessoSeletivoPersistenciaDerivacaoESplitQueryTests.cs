namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaPersistenciaDoProcessoSeletivo;

public sealed class ProcessoSeletivoPersistenciaDerivacaoESplitQueryTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly ApoioDaPersistenciaDoProcessoSeletivo _apoio;

    public ProcessoSeletivoPersistenciaDerivacaoESplitQueryTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPersistenciaDoProcessoSeletivo(fixture);
    }

    [Fact(DisplayName = "Redefinir a regra de derivação sobre o agregado carregado apaga a árvore antiga inteira (cascade órfão nos três níveis)")]
    public async Task RedefinirRegraDerivacao_SobreAgregadoTracked_ApagaArvoreAntiga()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — Redefinição", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirRegrasDerivacao([RegimeDeVagaComUmaRegraCondicionada()]).IsSuccess.Should().BeTrue();
        await _apoio.GravarAsync(processo);

        await SubstituirAConfiguracaoPelaRegraSemCondicaoAsync(processo.Id);

        await ConferirQueASobraEAUnicaRegraSemCondicaoAsync(processo.Id);
    }

    private static ConfiguracaoDerivacaoFato RegimeDeVagaComUmaRegraCondicionada() =>
        ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA",
        [
            RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!,
            RegraDerivacaoConfigurada.Criar(1, "AC_PCD",
                [CondicaoRegraDerivacao.Criar(1, "CONCORRER_PCD", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!,
        ]).Value!;

    /// <summary>
    /// Substitui a configuração por inteiro sobre o agregado JÁ tracked (fluxo real de reconfiguração):
    /// Clear+Add deve marcar as regras e condições antigas como órfãs e o cascade deve deletá-las.
    /// </summary>
    private async Task SubstituirAConfiguracaoPelaRegraSemCondicaoAsync(Guid processoId)
    {
        await using UniPlusDbContext mutateContext = _fixture.CreateDbContext();
        ProcessoSeletivo carregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(mutateContext).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        carregado.DefinirRegrasDerivacao(
            [ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA", [RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!]).Value!])
            .IsSuccess.Should().BeTrue();

        await mutateContext.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Contexto novo: a árvore antiga inteira sumiu — sobra só a única regra âncora sem condição.
    /// Contagens escopadas a ESTE processo (a fixture é compartilhada entre testes da classe).
    /// </summary>
    private async Task ConferirQueASobraEAUnicaRegraSemCondicaoAsync(Guid processoId)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        Guid configId = await readContext.Set<ConfiguracaoDerivacaoFato>()
            .Where(c => c.ProcessoSeletivoId == processoId).Select(c => c.Id).SingleAsync(CancellationToken.None);

        List<Guid> regraIds = await readContext.Set<RegraDerivacaoConfigurada>()
            .Where(r => r.ConfiguracaoDerivacaoFatoId == configId).Select(r => r.Id).ToListAsync(CancellationToken.None);
        regraIds.Should().HaveCount(1, "a regra AC_PCD antiga foi apagada como órfã");

        int condicoesRestantes = await readContext.Set<CondicaoRegraDerivacao>()
            .CountAsync(c => regraIds.Contains(c.RegraDerivacaoConfiguradaId), CancellationToken.None);
        condicoesRestantes.Should().Be(0, "a condição da regra AC_PCD antiga foi apagada em cascata");

        ProcessoSeletivo recarregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(readContext).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
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
        GrafoDeTodasAsColecoes grafo = MontarGrafoDeTodasAsColecoes();
        await _apoio.GravarAsync(grafo.Processo);
        CapturadorDeSql capturador = new();

        ProcessoSeletivo? recarregado = await _apoio.RecarregarCapturandoSqlAsync(grafo.Processo.Id, capturador);

        recarregado.Should().NotBeNull();
        capturador.Comandos.Where(c => c.Contains("SELECT", StringComparison.OrdinalIgnoreCase)).Should()
            .HaveCountGreaterThan(1,
                "AsSplitQuery() emite uma consulta própria por coleção incluída — um único SELECT indicaria que " +
                "a flag foi removida e o carregamento voltou a produzir o produto cartesiano num JOIN só");
        ConferirAsColecoesMaterializadas(recarregado!, grafo);
    }
}
