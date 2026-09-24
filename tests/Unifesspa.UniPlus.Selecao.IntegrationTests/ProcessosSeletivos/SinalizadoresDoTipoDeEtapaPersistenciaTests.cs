namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// O que o tipo de etapa admitia e a origem da nota no ENEM ficam congelados nas colunas do
/// snapshot da etapa, contra Postgres real: os valores sobrevivem à gravação, e a regravação
/// dos sinalizadores sobre a etapa tracked — uma instância nova do snapshot, mantida a
/// identidade — é persistida por change detection.
/// </summary>
public sealed class SinalizadoresDoTipoDeEtapaPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public SinalizadoresDoTipoDeEtapaPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Os sinalizadores congelados e a regravação deles sobre a etapa tracked sobrevivem ao banco")]
    public async Task Sinalizadores_PersistemERegravacaoTambem()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSVR", TipoProcesso.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        TipoEtapaSnapshot soElimina = TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise documental", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso etapa = EtapaProcesso.Criar("Análise documental", CaraterEtapa.Eliminatoria, soElimina, peso: null, notaMinima: 5m, ordem: 1).Value!;
        EtapaProcesso prova = EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 2).Value!;
        processo.DefinirEtapas([etapa, prova], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

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
            EtapaProcesso tracked = carregado.Etapas.Single(e => e.Id == etapa.Id);

            tracked.TipoEtapa.AdmitePontuacao.Should().BeFalse();
            tracked.TipoEtapa.AdmiteEliminacao.Should().BeTrue();

            // O caminho do handler quando o caráter da etapa muda e o tipo relido passou a pontuar.
            tracked.AtualizarDados(
                "Análise documental", CaraterEtapa.Ambas, tracked.TipoEtapa.ComSinalizadores(true, true), 1m, 5m, 1)
                .IsSuccess.Should().BeTrue();
            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo recarregado = (await new ProcessoSeletivoRepository(readContext, TimeProvider.System)
            .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
        TipoEtapaSnapshot regravado = recarregado.Etapas.Single(e => e.Id == etapa.Id).TipoEtapa;
        regravado.AdmitePontuacao.Should().BeTrue();
        regravado.AdmiteEliminacao.Should().BeTrue();
        regravado.Codigo.Should().Be("ANALISE_DOCUMENTAL");
        regravado.Nome.Should().Be("Análise documental");
        recarregado.Etapas.Single(e => e.Id == prova.Id).TipoEtapa.AdmitePontuacao.Should().BeTrue();
    }

    [Fact(DisplayName = "A nota de origem no ENEM congelada sobrevive ao banco e à regravação dos sinalizadores")]
    public async Task NotaDeOrigemNoEnem_PersisteERegravacaoAPreserva()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2027 — Medicina", TipoProcesso.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso doEnem = EtapaProcesso.Criar("Nota do ENEM", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, peso: 1m, ordem: 1).Value!;
        EtapaProcesso prova = EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 2).Value!;
        processo.DefinirEtapas([doEnem, prova], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

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
            EtapaProcesso tracked = carregado.Etapas.Single(e => e.Id == doEnem.Id);

            tracked.DeclaraNotaDoEnem.Should().BeTrue();
            carregado.Etapas.Single(e => e.Id == prova.Id).DeclaraNotaDoEnem.Should().BeFalse();

            tracked.AtualizarDados(
                "Nota do ENEM", CaraterEtapa.Classificatoria, tracked.TipoEtapa.ComSinalizadores(true, false), 1m, null, 1)
                .IsSuccess.Should().BeTrue();
            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo recarregado = (await new ProcessoSeletivoRepository(readContext, TimeProvider.System)
            .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
        TipoEtapaSnapshot regravado = recarregado.Etapas.Single(e => e.Id == doEnem.Id).TipoEtapa;
        regravado.AdmiteEliminacao.Should().BeFalse("a regravação dos sinalizadores chegou ao banco");
        regravado.NotaDeOrigemNoEnem.Should().BeTrue("a regravação dos sinalizadores preserva a identidade");
    }
}
