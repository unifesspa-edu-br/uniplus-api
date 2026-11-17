namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — PSVR", TiposDeProcessoDeTeste.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        TipoEtapaSnapshot soElimina = TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise documental", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Análise documental", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: soElimina, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: 5m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        EtapaProcesso prova = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 2, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapa, prova]).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (UniPlusDbContext configureContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(configureContext);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            EtapaProcesso tracked = carregado.Etapas.Single(e => e.Id == etapa.Id);

            tracked.TipoEtapa.AdmitePontuacao.Should().BeFalse();
            tracked.TipoEtapa.AdmiteEliminacao.Should().BeTrue();

            // O caminho do handler quando o caráter da etapa muda e o tipo relido passou a pontuar.
            tracked.AtualizarDados(
                new IdentificacaoDaEtapa(Nome: "Análise documental", Carater: CaraterEtapa.Ambas, TipoEtapa: tracked.TipoEtapa.ComSinalizadores(true, true, false), Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: 5m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null))
                .IsSuccess.Should().BeTrue();
            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo recarregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(readContext)
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2027 — Medicina", TiposDeProcessoDeTeste.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso doEnem = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        EtapaProcesso prova = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 2, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([doEnem, prova]).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (UniPlusDbContext configureContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(configureContext);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            EtapaProcesso tracked = carregado.Etapas.Single(e => e.Id == doEnem.Id);

            tracked.DeclaraNotaDoEnem.Should().BeTrue();
            carregado.Etapas.Single(e => e.Id == prova.Id).DeclaraNotaDoEnem.Should().BeFalse();

            tracked.AtualizarDados(
                new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: tracked.TipoEtapa.ComSinalizadores(true, false, false), Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null))
                .IsSuccess.Should().BeTrue();
            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo recarregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(readContext)
            .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
        TipoEtapaSnapshot regravado = recarregado.Etapas.Single(e => e.Id == doEnem.Id).TipoEtapa;
        regravado.AdmiteEliminacao.Should().BeFalse("a regravação dos sinalizadores chegou ao banco");
        regravado.NotaDeOrigemNoEnem.Should().BeTrue("a regravação dos sinalizadores preserva a identidade");
    }

    [Fact(DisplayName = "A correção independente congelada sobrevive ao banco e a regravação dos sinalizadores a relê")]
    public async Task CorrecaoIndependente_PersisteERegravacaoARele()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2028 — Redação", TiposDeProcessoDeTeste.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso redacao = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(
                Nome: "Redação", Carater: CaraterEtapa.Classificatoria,
                TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false, exigeCorrecaoIndependente: true).Value!,
                Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([redacao]).IsSuccess.Should().BeTrue();

        await using (UniPlusDbContext writeContext = _fixture.CreateDbContext())
        {
            await ProcessoSeletivoRepositoryDeTeste.Criar(writeContext).AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using (UniPlusDbContext configureContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivo carregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(configureContext)
                .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            EtapaProcesso tracked = carregado.Etapas.Single();
            tracked.TipoEtapa.ExigeCorrecaoIndependente.Should().BeTrue("o sinalizador congelado chegou ao banco");

            tracked.AtualizarDados(
                new IdentificacaoDaEtapa(Nome: "Redação", Carater: CaraterEtapa.Classificatoria, TipoEtapa: tracked.TipoEtapa.ComSinalizadores(true, true, false), Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null))
                .IsSuccess.Should().BeTrue();
            await configureContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivo recarregado = (await ProcessoSeletivoRepositoryDeTeste.Criar(readContext)
            .ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
        recarregado.Etapas.Single().TipoEtapa.ExigeCorrecaoIndependente.Should().BeFalse("a regravação dos sinalizadores chegou ao banco");
    }
}
