namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirClassificacaoCommandHandlerTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal").Value!;

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader RegraCatalogoReader,
        IPesoAreaEnemReader PesoAreaEnemReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(
            repository, Substitute.For<IRegraCatalogoReader>(), Substitute.For<IPesoAreaEnemReader>(), Substitute.For<ISelecaoUnitOfWork>());
    }

    private static void MockRegrasBasicas(Mocks mocks)
    {
        mocks.RegraCatalogoReader.ObterAsync(RegraCalculoCodigo.FormulaMediaPonderada, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraCalculoCodigo.FormulaMediaPonderada, TipoRegra.RegraCalculo));
        mocks.RegraCatalogoReader.ObterAsync(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraArredondamentoCodigo.PrecisaoTruncar, TipoRegra.RegraArredondamento));
        mocks.RegraCatalogoReader.ObterAsync(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, TipoRegra.RegraOrdemAlocacao));
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirClassificacaoCommand command = new(
            Guid.CreateVersion7(), "X", "v1", null, null, null, "Y", "v1", 1, [], false, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com FORMULA-MEDIA-PONDERADA + arredondamento resolve e persiste")]
    public async Task Handle_MediaPonderadaComArredondamento_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.FormulaMediaPonderada, "v1",
            RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1,
            [], false, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao.Should().NotBeNull();
        processo.Classificacao!.RegraArredondamento.Should().NotBeNull();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Handle repassa BaseadoEmEnem para Criar — chega até a configuração persistida (#850)")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_PropagaBaseadoEmEnem(bool baseadoEmEnem)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);

        // Classificação baseada em ENEM com cálculo local exige a resolução de Pesos por Área.
        string? resolucao = baseadoEmEnem ? ResolucaoDePesos : null;
        mocks.PesoAreaEnemReader.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>())
            .Returns(ResolucaoCom(GruposDoAnexoI));

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.FormulaMediaPonderada, "v1",
            RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1,
            [], baseadoEmEnem, resolucao, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.BaseadoEmEnem.Should().Be(baseadoEmEnem,
            "o rótulo TipoProcesso (PSIQ, não-ENEM) não pode sobrepor o dado declarado no command");
    }

    [Fact(DisplayName = "Handle com CLASSIFICACAO-IMPORTADA sem arredondamento resolve e persiste (INV-B8)")]
    public async Task Handle_Importada_SemArredondamento_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("SiSU 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraCalculoCodigo.ClassificacaoImportada, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraCalculoCodigo.ClassificacaoImportada, TipoRegra.RegraCalculo));
        mocks.RegraCatalogoReader.ObterAsync(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, TipoRegra.RegraOrdemAlocacao));

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.ClassificacaoImportada, "v1",
            null, null, null,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 2,
            [], false, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao!.RegraArredondamento.Should().BeNull();
    }

    [Fact(DisplayName = "Handle com ELIM-NOTA-MINIMA-ETAPA resolve args e persiste")]
    public async Task Handle_ComEliminacaoNotaMinimaEtapa_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Convênios 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso etapa = EtapaProcesso.Criar("Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true).Value!, peso: 1m, ordem: 1).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente);

        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.RegraCatalogoReader.ObterAsync(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, TipoRegra.RegraEliminacao));

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.FormulaMediaPonderada, "v1",
            RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1,
            [new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", etapa.Id, 4m, null)], false, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        RegraEliminacao eliminacao = processo.Classificacao!.RegrasEliminacao.Single();
        ((ArgsElimNotaMinimaEtapa)eliminacao.Args).EtapaRef.Should().Be(etapa.Id);
    }

    [Fact(DisplayName = "Handle com ELIM-NOTA-MINIMA-ETAPA sem EtapaRef/NotaMinima recusa")]
    public async Task Handle_EliminacaoSemEtapaRef_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.RegraCatalogoReader.ObterAsync(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, TipoRegra.RegraEliminacao));

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.FormulaMediaPonderada, "v1",
            RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1,
            [new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", null, null, null)], false, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.EtapaRefENotaMinimaObrigatorios");
    }

    [Fact(DisplayName = "Handle com ELIM-ZERO-EM-AREA e Minimo estranho ao args recusa (payload contraditório)")]
    public async Task Handle_ZeroEmAreaComArgsEstranhos_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("SiSU 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.RegraCatalogoReader.ObterAsync(RegraEliminacaoCodigo.ElimZeroEmArea, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraEliminacaoCodigo.ElimZeroEmArea, TipoRegra.RegraEliminacao));

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.FormulaMediaPonderada, "v1",
            RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1,
            [new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimZeroEmArea, "v1", null, null, 400m)], false, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.ArgsIncompativeisComRegra");
    }

    [Fact(DisplayName = "Handle com regra de cálculo inexistente recusa")]
    public async Task Handle_RegraCalculoNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirClassificacaoCommand command = new(
            processo.Id, "INEXISTENTE", "v1", null, null, null, RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1, [], false, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoClassificacao.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de cálculo de tipo inválido recusa")]
    public async Task Handle_RegraCalculoTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraCalculoCodigo.FormulaMediaPonderada, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraCalculoCodigo.FormulaMediaPonderada, TipoRegra.RegraArredondamento));

        DefinirClassificacaoCommand command = new(
            processo.Id, RegraCalculoCodigo.FormulaMediaPonderada, "v1", null, null, null,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1, [], false, null, PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoClassificacao.RegraTipoInvalido");
    }

    private const string ResolucaoDePesos = "Res. 805/2024";

    private static readonly (string Codigo, string Rotulo)[] GruposDoAnexoI =
    [
        ("HUMANISTICA_I", "Humanística I"),
        ("HUMANISTICA_II", "Humanística II"),
        ("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"),
        ("TECNOLOGICA", "Tecnológica"),
    ];

    private static PesoAreaEnemView LinhaDoGrupo((string Codigo, string Rotulo) grupo) =>
        new(
            Guid.CreateVersion7(),
            ResolucaoDePesos,
            new GrupoAreaEnemView(grupo.Codigo, grupo.Rotulo),
            [
                new PesoAreaEnemAreaView("REDACAO", "Redação", 2.00m, 400m),
                new PesoAreaEnemAreaView("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, null),
                new PesoAreaEnemAreaView("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, null),
                new PesoAreaEnemAreaView("LINGUAGENS", "Linguagens e suas Tecnologias", 2.50m, null),
                new PesoAreaEnemAreaView("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, null),
            ],
            $"Resolução nº 805/2024/Consepe – Anexo I ({grupo.Rotulo})");

    private static ResolucaoPesoAreaEnemView ResolucaoCom(
        IEnumerable<(string Codigo, string Rotulo)> presentes,
        params (string Codigo, string Rotulo)[] ausentes) =>
        new(
            ResolucaoDePesos,
            [.. presentes.Select(LinhaDoGrupo)],
            [.. ausentes.Select(static g => new GrupoAreaEnemView(g.Codigo, g.Rotulo))]);

    private static DefinirClassificacaoCommand ComandoEnemLocal(
        Guid processoId, string? resolucao, bool baseadoEmEnem = true, string regraCalculo = RegraCalculoCodigo.FormulaMediaPonderada) =>
        regraCalculo == RegraCalculoCodigo.ClassificacaoImportada
            ? new(processoId, regraCalculo, "v1", null, null, null,
                RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1, [], baseadoEmEnem, resolucao, PrecondicaoIfMatch.Ausente)
            : new(processoId, regraCalculo, "v1", RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
                RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1, [], baseadoEmEnem, resolucao, PrecondicaoIfMatch.Ausente);

    private static ProcessoSeletivo NovoProcessoEnem() =>
        ProcessoSeletivo.Criar("PS Medicina 2027", TipoProcesso.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    [Fact(DisplayName = "Resolução completa é congelada por cópia: grupo, base legal e peso e corte de cada área")]
    public async Task Handle_ResolucaoCompleta_CongelaOQuadro()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.PesoAreaEnemReader.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>())
            .Returns(ResolucaoCom(GruposDoAnexoI));

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, ResolucaoDePesos), mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        ConfiguracaoClassificacao classificacao = processo.Classificacao!;
        classificacao.ResolucaoPesoAreaEnem.Should().Be(ResolucaoDePesos);
        classificacao.QuadroPesoAreaEnem.Select(g => (g.GrupoAreaEnem.Codigo, g.GrupoAreaEnem.Rotulo))
            .Should().BeEquivalentTo(GruposDoAnexoI);

        GrupoPesoAreaEnemCongelado saude = classificacao.QuadroPesoAreaEnem.Single(g => g.GrupoAreaEnem.Codigo == "SAUDE_E_BIOLOGICAS");
        saude.BaseLegal.Should().Be("Resolução nº 805/2024/Consepe – Anexo I (Saúde e Biológicas)");
        saude.Areas.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().BeEquivalentTo(
        [
            ("REDACAO", "Redação", 2.00m, (decimal?)400m),
            ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, (decimal?)null),
            ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, (decimal?)null),
            ("LINGUAGENS", "Linguagens e suas Tecnologias", 2.50m, (decimal?)null),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, (decimal?)null),
        ]);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resolução sem algum grupo vivo é recusada no campo da resolução, nomeando o grupo ausente")]
    public async Task Handle_ResolucaoIncompleta_RecusaNomeandoOGrupoAusente()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.PesoAreaEnemReader.ObterPorResolucaoAsync(ResolucaoDePesos, Arg.Any<CancellationToken>())
            .Returns(ResolucaoCom(GruposDoAnexoI.Where(g => g.Codigo != "SAUDE_E_BIOLOGICAS"), ("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas")));

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, ResolucaoDePesos), mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("resolucaoPesoAreaEnem");
        erro.Error.Code.Should().Be("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIncompleta");
        erro.Error.Message.Should().Contain("Saúde e Biológicas");
        processo.Classificacao.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resolução sem nenhuma linha viva é recusada no campo da resolução")]
    public async Task Handle_ResolucaoInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.PesoAreaEnemReader.ObterPorResolucaoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ResolucaoPesoAreaEnemView?)null);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, "Res. inexistente"), mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("resolucaoPesoAreaEnem");
        erro.Error.Code.Should().Be("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada");
        processo.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Classificação baseada em ENEM com cálculo local sem resolução é recusada sem consultar o cadastro")]
    public async Task Handle_EnemLocalSemResolucao_RecusaSemConsultarOCadastro()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, resolucao: "   "), mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e =>
            e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria");
        await mocks.PesoAreaEnemReader.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Theory(DisplayName = "Resolução com caractere invisível ou acima da coluna é recusada como inválida sem consultar o cadastro")]
    [InlineData("805\u0000")]
    [InlineData("Res.\n805/2024")]
    [InlineData("Res. \u202E4202/508")]
    [InlineData("Resolução com quarenta e um caracteres ..")]
    public async Task Handle_ResolucaoMalformada_RecusaSemConsultarOCadastro(string resolucao)
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, resolucao), mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e =>
            e.Field == ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem
            && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");
        await mocks.PesoAreaEnemReader.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Fact(DisplayName = "Resolução com não-caractere é recusada como inválida sem consultar o cadastro, sem exceção")]
    public async Task Handle_ResolucaoComNaoCaractere_RecusaSemConsultarOCadastro()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, "Res. 805" + (char)0xFFFE), mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");
        await mocks.PesoAreaEnemReader.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Fact(DisplayName = "ADR-0125: a forma inválida da resolução sai junto com as demais violações, uma vez só")]
    public async Task Handle_ResolucaoMalformadaECasasInvalidas_AcumulaSemDuplicar()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        DefinirClassificacaoCommand command = ComandoEnemLocal(processo.Id, "805\u0000") with { CasasArredondamento = 0 };

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ((string?)ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida"),
            ((string?)"casasArredondamento", "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio"),
        ]);
        await mocks.PesoAreaEnemReader.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Fact(DisplayName = "Resolução acima da coluna em classificação importada é recusada só como indevida")]
    public async Task Handle_ResolucaoLongaEmImportada_RecusaSoComoIndevida()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraCalculoCodigo.ClassificacaoImportada, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraCalculoCodigo.ClassificacaoImportada, TipoRegra.RegraCalculo));
        mocks.RegraCatalogoReader.ObterAsync(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, TipoRegra.RegraOrdemAlocacao));

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, "Resolução com quarenta e um caracteres ..", baseadoEmEnem: true, RegraCalculoCodigo.ClassificacaoImportada),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.Errors.Select(e => e.Error.Code).Should().Equal(
            ["ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida"],
            "a classificação importada não admite resolução nenhuma, e é isso que o operador precisa saber");
        await mocks.PesoAreaEnemReader.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Fact(DisplayName = "A resolução gravada é a forma que o cadastro devolveu, não o texto do comando")]
    public async Task Handle_GravaAResolucaoQueOCadastroDevolveu()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.PesoAreaEnemReader.ObterPorResolucaoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResolucaoCom(GruposDoAnexoI) with { Resolucao = "Resolução canônica" });

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, "texto do comando"), mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.ResolucaoPesoAreaEnem.Should().Be("Resolução canônica");
    }

    [Fact(DisplayName = "ADR-0125: a recusa do cadastro sai junto com as demais violações da classificação")]
    public async Task Handle_ResolucaoInexistenteECasasInvalidas_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.PesoAreaEnemReader.ObterPorResolucaoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ResolucaoPesoAreaEnemView?)null);
        DefinirClassificacaoCommand command = ComandoEnemLocal(processo.Id, "Res. inexistente") with { CasasArredondamento = 0 };

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ((string?)"resolucaoPesoAreaEnem", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada"),
            ((string?)"casasArredondamento", "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio"),
        ], "o quadro vazio é consequência da recusa do cadastro, e não sai como violação à parte");
        processo.Classificacao.Should().BeNull();
    }

    [Theory(DisplayName = "Resolução informada fora da classificação baseada em ENEM com cálculo local é recusada sem consultar o cadastro")]
    [InlineData(false, RegraCalculoCodigo.FormulaMediaPonderada)]
    [InlineData(true, RegraCalculoCodigo.ClassificacaoImportada)]
    [InlineData(false, RegraCalculoCodigo.ClassificacaoImportada)]
    public async Task Handle_ResolucaoForaDeEnemLocal_RecusaComoIndevida(bool baseadoEmEnem, string regraCalculo)
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.RegraCatalogoReader.ObterAsync(RegraCalculoCodigo.ClassificacaoImportada, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraCalculoCodigo.ClassificacaoImportada, TipoRegra.RegraCalculo));

        Result<MutacaoAceita> result = await DefinirClassificacaoCommandHandler.Handle(
            ComandoEnemLocal(processo.Id, ResolucaoDePesos, baseadoEmEnem, regraCalculo),
            mocks.Repository, mocks.RegraCatalogoReader, mocks.PesoAreaEnemReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e =>
            e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida");
        await mocks.PesoAreaEnemReader.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }
}
