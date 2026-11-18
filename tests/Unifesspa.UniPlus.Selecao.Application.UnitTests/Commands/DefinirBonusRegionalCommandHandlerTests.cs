namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirBonusRegionalCommandHandlerTests
{
    private static readonly Guid BaseLegalId = Guid.CreateVersion7();

    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal");

    private static BaseLegalBonusRegionalView BaseLegalValida() => new(
        BaseLegalId,
        "PORTARIA_UNIFESSPA_NO_2514_2023",
        "PORTARIA",
        "Portaria Unifesspa nº 2514/2023",
        "Institui inclusão regional",
        [new BaseLegalBonusRegionalMunicipioView("1504208", "Marabá", "PA")]);

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader RegraCatalogoReader,
        IBaseLegalBonusRegionalReader BaseLegalBonusRegionalReader,
        IUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        IBaseLegalBonusRegionalReader baseLegalBonusRegionalReader = Substitute.For<IBaseLegalBonusRegionalReader>();
        baseLegalBonusRegionalReader.ObterPorIdAsync(BaseLegalId, Arg.Any<CancellationToken>()).Returns(BaseLegalValida());
        return new Mocks(repository, Substitute.For<IRegraCatalogoReader>(), baseLegalBonusRegionalReader, Substitute.For<IUnitOfWork>());
    }

    private static Task<Result> Handle(DefinirBonusRegionalCommand command, Mocks mocks) =>
        DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.BaseLegalBonusRegionalReader, mocks.UnitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirBonusRegionalCommand command = new(Guid.CreateVersion7(), false, null, null, null, null, null, null);

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle declarando que não aplica o bônus remove a configuração, declara e persiste (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015)")]
    public async Task Handle_NaoAplica_RemoveBonusEDeclara()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirBonusRegional(aplica: true, bonus: ConfiguracaoBonusRegional.Criar(Domain.ValueObjects.ReferenciaRegra.De(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)), 1.20m, null, null, new BaseLegalCongelada(BaseLegalId, "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!);

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirBonusRegionalCommand command = new(processo.Id, false, null, null, null, null, null, null);

        Result result = await Handle(command, mocks);

        result.IsSuccess.Should().BeTrue();
        processo.BonusRegional.Should().BeNull();
        processo.AplicaBonusRegional.Should().BeFalse();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle aplicando o bônus sem informar a configuração recusa com erro nomeado, e não persiste")]
    public async Task Handle_AplicaSemConfiguracao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await Handle(
            new DefinirBonusRegionalCommand(processo.Id, true, null, null, null, null, null, null), mocks);

        result.Error!.Code.Should().Be(Domain.Errors.ProcessoSeletivoErrorCodes.BonusRegionalAplicaSemConfiguracao);
        processo.AplicaBonusRegional.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Theory(DisplayName = "Handle declarando que não aplica, mas enviando qualquer campo do bônus, recusa com erro nomeado")]
    [InlineData(null, "v1", null, null, false)]
    [InlineData(null, null, 1.20, null, false)]
    [InlineData(null, null, null, 0.5, false)]
    [InlineData(null, null, null, null, true)]
    public async Task Handle_NaoAplicaComCampoSolto_Recusa(string? regraCodigo, string? regraVersao, double? fator, double? teto, bool comBaseLegal)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirBonusRegionalCommand command = new(
            processo.Id, false, regraCodigo, regraVersao, (decimal?)fator, (decimal?)teto, null, comBaseLegal ? BaseLegalId : null);

        Result result = await Handle(command, mocks);

        result.Error!.Code.Should().Be(Domain.Errors.ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao);
        processo.AplicaBonusRegional.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Handle declarando que não aplica, mas enviando a configuração, recusa com erro nomeado")]
    public async Task Handle_NaoAplicaComConfiguracao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await Handle(
            new DefinirBonusRegionalCommand(processo.Id, false, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, null, BaseLegalId), mocks);

        result.Error!.Code.Should().Be(Domain.Errors.ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao);
        processo.AplicaBonusRegional.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Handle declarando que não aplica, mas citando o fato de elegibilidade, recusa com erro nomeado")]
    public async Task Handle_NaoAplicaComFatoDeElegibilidade_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await Handle(
            new DefinirBonusRegionalCommand(processo.Id, false, null, null, null, null, "PEDE_O_ACRESCIMO", null), mocks);

        result.Error!.Code.Should().Be(Domain.Errors.ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao);
        processo.AplicaBonusRegional.Should().BeNull();
    }

    [Fact(DisplayName = "Handle com regra e base legal válidas define o bônus, congela o snapshot e persiste")]
    public async Task Handle_RegraEBaseLegalValidas_DefineBonus()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Convênios 2026", TiposDeProcessoDeTeste.PSVR, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraBonus));

        DefinirBonusRegionalCommand command = new(processo.Id, true, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, "PEDE_O_ACRESCIMO", BaseLegalId);

        Result result = await Handle(command, mocks);

        result.IsSuccess.Should().BeTrue();
        processo.BonusRegional.Should().NotBeNull();
        processo.BonusRegional!.Fator.Should().Be(1.20m);
        processo.BonusRegional.FatoElegibilidadeCodigo.Should().Be("PEDE_O_ACRESCIMO");
        processo.BonusRegional.BaseLegalBonusRegionalId.Should().Be(BaseLegalId);
        processo.BonusRegional.TipoInstrumento.Should().Be("PORTARIA");
        processo.BonusRegional.Identificacao.Should().Be("Portaria Unifesspa nº 2514/2023");
        processo.BonusRegional.Municipios.Should().ContainSingle(m => m.CodigoIbge == "1504208");
    }

    [Fact(DisplayName = "Handle com RegraCodigo informado mas sem Fator recusa")]
    public async Task Handle_SemFator_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirBonusRegionalCommand command = new(processo.Id, true, RegraBonusCodigo.Multiplicativo, "v1", null, null, null, BaseLegalId);

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.CamposObrigatorios");
    }

    [Fact(DisplayName = "Handle com RegraCodigo informado mas sem BaseLegalBonusRegionalId recusa, sem consultar o reader")]
    public async Task Handle_SemBaseLegalBonusRegionalId_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirBonusRegionalCommand command = new(processo.Id, true, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, null, null);

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.CamposObrigatorios");
        await mocks.BaseLegalBonusRegionalReader.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com regra inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirBonusRegionalCommand command = new(processo.Id, true, "INEXISTENTE", "v1", 1.20m, null, null, BaseLegalId);

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de regra_bonus recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraCalculo));

        DefinirBonusRegionalCommand command = new(processo.Id, true, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, null, BaseLegalId);

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.RegraTipoInvalido");
    }

    [Fact(DisplayName = "Handle com Base Legal inexistente ou desativada recusa (CA-02) sem persistir")]
    public async Task Handle_BaseLegalNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraBonus));
        Guid idInexistente = Guid.CreateVersion7();
        mocks.BaseLegalBonusRegionalReader.ObterPorIdAsync(idInexistente, Arg.Any<CancellationToken>())
            .Returns((BaseLegalBonusRegionalView?)null);

        DefinirBonusRegionalCommand command = new(processo.Id, true, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, null, idInexistente);

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.BaseLegalNaoEncontrada");
        processo.BonusRegional.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName =
        "Handle com processo já publicado propaga MutacaoPosPublicacaoBloqueada e NÃO persiste (CA-04)")]
    public async Task Handle_ProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraBonus));

        DefinirBonusRegionalCommand command = new(processo.Id, true, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, null, BaseLegalId);

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.BonusRegional.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName =
        "Handle de remoção (RegraCodigo nulo) com processo já publicado propaga bloqueio e NÃO persiste (CA-04)")]
    public async Task Handle_RemocaoComProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirBonusRegionalCommand command = new(processo.Id, false, null, null, null, null, null, null);

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
