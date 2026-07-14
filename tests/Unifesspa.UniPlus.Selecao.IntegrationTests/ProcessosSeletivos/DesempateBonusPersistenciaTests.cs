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
/// Cobertura de integração (Postgres real via Testcontainers) do bônus
/// regional (RN05) e dos critérios de desempate (Story #774, modelagem
/// P-B §2.5/§2.6): persiste e recarrega as 4 variantes de
/// <c>ArgsCriterioDesempate</c> (jsonb polimórfico) e o owned type
/// <c>ReferenciaRegra</c>, e prova a reconfiguração sobre o agregado tracked
/// (mesma proteção <c>ValueGeneratedNever</c> da F0).
/// </summary>
public sealed class DesempateBonusPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public DesempateBonusPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ReferenciaRegra Regra(string codigo, string hashSeed) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashSeed[0], 64)).Value!;

    [Fact(DisplayName = "Persiste e recarrega os 4 critérios de desempate (args polimórficos) e o bônus regional")]
    public async Task PersisteERecarrega_DesempateEBonus()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Convênios 2026", TipoProcesso.PSVR);
        EtapaProcesso etapa = EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        CriterioDesempate maiorNotaEtapa = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, "a"), new ArgsDesempateMaiorNotaEtapa(etapa.Id)).Value!;
        CriterioDesempate idoso = CriterioDesempate.Criar(
            2, Regra(CriterioDesempateCodigo.Idoso, "b"), new ArgsDesempateIdoso(60)).Value!;
        CriterioDesempate maiorIdade = CriterioDesempate.Criar(
            3, Regra(CriterioDesempateCodigo.MaiorIdade, "c"), new ArgsDesempateMaiorIdade()).Value!;
        CriterioDesempate predicadoFato = CriterioDesempate.Criar(
            4, Regra(CriterioDesempateCodigo.PredicadoFato, "d"), new ArgsDesempatePredicadoFato("PROFESSOR_RURAL", "IGUAL", "S")).Value!;

        Result desempateResult = processo.DefinirCriteriosDesempate([maiorNotaEtapa, idoso, maiorIdade, predicadoFato], PrecondicaoIfMatch.Ausente);
        desempateResult.IsSuccess.Should().BeTrue();

        ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(
            Regra(RegraBonusCodigo.Multiplicativo, "e"), 1.20m, null, "Marabá", "RN05 + decisão PO Jairo").Value!;
        processo.DefinirBonusRegional(bonus, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.BonusRegional)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.CriteriosDesempate.Should().HaveCount(4);

        CriterioDesempate maiorNotaEtapaRecarregado = recarregado.CriteriosDesempate.Single(c => c.Ordem == 1);
        maiorNotaEtapaRecarregado.Regra.Codigo.Should().Be(CriterioDesempateCodigo.MaiorNotaEtapa);
        ((ArgsDesempateMaiorNotaEtapa)maiorNotaEtapaRecarregado.Args).EtapaRef.Should().Be(etapa.Id);

        CriterioDesempate idosoRecarregado = recarregado.CriteriosDesempate.Single(c => c.Ordem == 2);
        ((ArgsDesempateIdoso)idosoRecarregado.Args).IdadeMinima.Should().Be(60);

        CriterioDesempate maiorIdadeRecarregado = recarregado.CriteriosDesempate.Single(c => c.Ordem == 3);
        maiorIdadeRecarregado.Args.Should().BeOfType<ArgsDesempateMaiorIdade>();

        CriterioDesempate predicadoFatoRecarregado = recarregado.CriteriosDesempate.Single(c => c.Ordem == 4);
        ArgsDesempatePredicadoFato predicadoArgs = (ArgsDesempatePredicadoFato)predicadoFatoRecarregado.Args;
        predicadoArgs.Fato.Should().Be("PROFESSOR_RURAL");
        predicadoArgs.Operador.Should().Be("IGUAL");
        predicadoArgs.Valor.Should().Be("S");

        recarregado.BonusRegional.Should().NotBeNull();
        recarregado.BonusRegional!.Regra.Codigo.Should().Be(RegraBonusCodigo.Multiplicativo);
        recarregado.BonusRegional.Fator.Should().Be(1.20m);
        recarregado.BonusRegional.Teto.Should().BeNull();
        recarregado.BonusRegional.MunicipioConvenio.Should().Be("Marabá");
    }

    [Fact(DisplayName = "Reconfigurar desempate+bônus sobre o agregado tracked insere os filhos novos, não falha em UPDATE")]
    public async Task ReconfigurarDesempateBonusSobreAgregadoTracked_InsereFilhos()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026 — Reconfig", TipoProcesso.PSIQ);
        processo.DefinirEtapas([EtapaProcesso.Criar("Entrevista", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)], PrecondicaoIfMatch.Ausente);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorIdade, "a"), new ArgsDesempateMaiorIdade()).Value!], PrecondicaoIfMatch.Ausente);
        processo.DefinirBonusRegional(
            ConfiguracaoBonusRegional.Criar(Regra(RegraBonusCodigo.Multiplicativo, "b"), 1.10m, null, null, null).Value!, PrecondicaoIfMatch.Ausente);

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

            Result desempateResult = carregado.DefinirCriteriosDesempate(
            [
                CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.Idoso, "c"), new ArgsDesempateIdoso(65)).Value!,
                CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.MaiorIdade, "d"), new ArgsDesempateMaiorIdade()).Value!,
            ], PrecondicaoIfMatch.Ausente);
            desempateResult.IsSuccess.Should().BeTrue();

            Result bonusResult = carregado.DefinirBonusRegional(
                ConfiguracaoBonusRegional.Criar(Regra(RegraBonusCodigo.Multiplicativo, "e"), 1.30m, 5m, "Marabá", null).Value!, PrecondicaoIfMatch.Ausente);
            bonusResult.IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.BonusRegional)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.CriteriosDesempate.Should().HaveCount(2);
        recarregado.BonusRegional!.Fator.Should().Be(1.30m);
        recarregado.BonusRegional.Teto.Should().Be(5m);
    }

    [Fact(DisplayName = "Atualizar dados da MESMA etapa (Id preservado) mantém o desempate referenciando-a (achado Codex)")]
    public async Task AtualizarEtapaMesmoId_MantemDesempateReferenciado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSVR", TipoProcesso.PSVR);
        EtapaProcesso etapa = EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, "a"), new ArgsDesempateMaiorNotaEtapa(etapa.Id)).Value!], PrecondicaoIfMatch.Ausente);

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
            // etapa_ref do desempate ficaria órfão a cada PUT /etapas.
            EtapaProcesso etapaTracked = carregado.Etapas.Single();
            etapaTracked.AtualizarDados("Redação (revisada)", CaraterEtapa.Classificatoria, 2m, null, 1);

            Result result = carregado.DefinirEtapas([etapaTracked], PrecondicaoIfMatch.Ausente);
            result.IsSuccess.Should().BeTrue();

            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo? recarregado = await readContext.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.CriteriosDesempate)
            .FirstOrDefaultAsync(p => p.Id == processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        EtapaProcesso etapaAtualizada = recarregado!.Etapas.Single();
        etapaAtualizada.Id.Should().Be(etapa.Id);
        etapaAtualizada.Nome.Should().Be("Redação (revisada)");
        etapaAtualizada.Peso.Should().Be(2m);

        CriterioDesempate criterio = recarregado.CriteriosDesempate.Single();
        ((ArgsDesempateMaiorNotaEtapa)criterio.Args).EtapaRef.Should().Be(etapa.Id);
    }
}
