namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirFadaCommandHandlerTests
{
    private static readonly Guid BaseLegalId = Guid.CreateVersion7();

    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, Json("{}"), Json("[]"), "base legal");

    private static BaseLegalDoFadaView BaseLegalValida() => new(
        BaseLegalId,
        "PORTARIA_UNIFESSPA_NO_2514_2023",
        "PORTARIA",
        "Portaria Unifesspa nº 2514/2023",
        "Institui inclusão regional",
        [new MunicipioDaBaseLegalDoFadaView("1504208", "Marabá", "PA")]);

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader RegraCatalogoReader,
        IBaseLegalDoFadaReader BaseLegalDoFadaReader,
        IUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        IBaseLegalDoFadaReader baseLegalDoFadaReader = Substitute.For<IBaseLegalDoFadaReader>();
        baseLegalDoFadaReader.ObterPorIdAsync(BaseLegalId, Arg.Any<CancellationToken>()).Returns(BaseLegalValida());
        return new Mocks(repository, Substitute.For<IRegraCatalogoReader>(), baseLegalDoFadaReader, Substitute.For<IUnitOfWork>());
    }

    private static Task<Result> Handle(DefinirFadaCommand command, Mocks mocks) =>
        DefinirFadaCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.BaseLegalDoFadaReader, mocks.UnitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirFadaCommand command = FadaDeTeste.Para(Guid.CreateVersion7()).Construir();

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle sem a declaração de aplicação recusa como obrigatória no campo aplica, mantém o processo e não persiste")]
    public async Task Handle_AplicaAusente_RecusaComoObrigatorio()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS FADA ausente");
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFadaCommand command = new(processo.Id, null, null, null, null, null, null);

        Result result = await Handle(command, mocks);

        result.Errors.Should().ContainSingle(e => e.Field == "aplica" && e.Error.Code == "uniplus.validacao.obrigatorio");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle declarando que não aplica o FADA remove a configuração, declara e persiste (UNI-REQ-0015)")]
    public async Task Handle_NaoAplica_RemoveFadaEDeclara()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirFada(aplica: true, configuracao: ConfiguracaoDoFada.Criar(Domain.ValueObjects.ReferenciaRegra.De(RegraDoFada.Multiplicativo.Codigo(), "v1", new string('a', 64)), 1.20m, null, null, new BaseLegalCongelada(BaseLegalId, "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!);

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).Construir();

        Result result = await Handle(command, mocks);

        result.IsSuccess.Should().BeTrue();
        processo.Fada.Should().BeNull();
        processo.AplicaFada.Should().BeFalse();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle aplicando o FADA sem informar a configuração recusa com erro nomeado, e não persiste")]
    public async Task Handle_AplicaSemConfiguracao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await Handle(
            FadaDeTeste.Para(processo.Id).QueAplica().Construir(), mocks);

        result.Error!.Code.Should().Be(Domain.Errors.ProcessoSeletivoErrorCodes.FadaAplicaSemConfiguracao);
        processo.AplicaFada.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Theory(DisplayName = "Handle declarando que não aplica, mas enviando qualquer campo do FADA, recusa com erro nomeado")]
    [InlineData(null, "v1", null, null, false)]
    [InlineData(null, null, 1.20, null, false)]
    [InlineData(null, null, null, 0.5, false)]
    [InlineData(null, null, null, null, true)]
    public async Task Handle_NaoAplicaComCampoSolto_Recusa(string? regraCodigo, string? regraVersao, double? fator, double? teto, bool comBaseLegal)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        FadaDeTeste pedido = FadaDeTeste.Para(processo.Id).ComFator((decimal?)fator).ComTeto((decimal?)teto);
        pedido = regraCodigo is null && regraVersao is null ? pedido : pedido.ComRegra(regraCodigo, regraVersao);
        DefinirFadaCommand command = (comBaseLegal ? pedido.ComBaseLegal(BaseLegalId) : pedido).Construir();

        Result result = await Handle(command, mocks);

        result.Error!.Code.Should().Be(Domain.Errors.ProcessoSeletivoErrorCodes.FadaNaoAplicaComConfiguracao);
        processo.AplicaFada.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Handle declarando que não aplica, mas enviando a configuração, recusa com erro nomeado")]
    public async Task Handle_NaoAplicaComConfiguracao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await Handle(
            FadaDeTeste.Para(processo.Id).ComRegra(RegraDoFada.Multiplicativo.Codigo()).ComFator(1.20m).ComBaseLegal(BaseLegalId).Construir(), mocks);

        result.Error!.Code.Should().Be(Domain.Errors.ProcessoSeletivoErrorCodes.FadaNaoAplicaComConfiguracao);
        processo.AplicaFada.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Handle declarando que não aplica, mas citando o fato de elegibilidade, recusa com erro nomeado")]
    public async Task Handle_NaoAplicaComFatoDeElegibilidade_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await Handle(
            FadaDeTeste.Para(processo.Id).ComFatoDeElegibilidade("PEDE_O_ACRESCIMO").Construir(), mocks);

        result.Error!.Code.Should().Be(Domain.Errors.ProcessoSeletivoErrorCodes.FadaNaoAplicaComConfiguracao);
        processo.AplicaFada.Should().BeNull();
    }

    private static async Task<(ProcessoSeletivo Processo, Result Resultado)> DefinirFadaValidaAsync()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Convênios 2026", TiposDeProcessoDeTeste.VagasRemanescentes, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        ComRegraDoFadaNoCatalogo(mocks);
        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).QueAplica().ComRegra(RegraDoFada.Multiplicativo.Codigo())
            .ComFator(1.20m).ComFatoDeElegibilidade("PEDE_O_ACRESCIMO").ComBaseLegal(BaseLegalId).Construir();

        return (processo, await Handle(command, mocks));
    }

    private static void ComRegraDoFadaNoCatalogo(Mocks mocks) =>
        mocks.RegraCatalogoReader.ObterAsync(RegraDoFada.Multiplicativo.Codigo(), "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraDoFada.Multiplicativo.Codigo(), TipoRegra.RegraFada));

    [Fact(DisplayName = "Handle com regra e base legal válidas define o FADA e persiste")]
    public async Task Handle_RegraEBaseLegalValidas_DefineFada()
    {
        (ProcessoSeletivo processo, Result result) = await DefinirFadaValidaAsync();

        result.IsSuccess.Should().BeTrue();
        processo.Fada.Should().NotBeNull();
        processo.Fada!.Fator.Should().Be(1.20m);
        processo.Fada.FatoElegibilidadeCodigo.Should().Be("PEDE_O_ACRESCIMO");
        processo.Fada.BaseLegalId.Should().Be(BaseLegalId);
    }

    [Fact(DisplayName = "Handle com regra e base legal válidas congela o instrumento e os municípios da base legal")]
    public async Task Handle_RegraEBaseLegalValidas_CongelaOSnapshotDaBaseLegal()
    {
        (ProcessoSeletivo processo, _) = await DefinirFadaValidaAsync();

        processo.Fada!.TipoInstrumento.Should().Be("PORTARIA");
        processo.Fada.Identificacao.Should().Be("Portaria Unifesspa nº 2514/2023");
        processo.Fada.Municipios.Should().ContainSingle(m => m.CodigoIbge == "1504208");
    }

    [Fact(DisplayName = "Handle com RegraCodigo informado mas sem Fator recusa")]
    public async Task Handle_SemFator_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).QueAplica().ComRegra(RegraDoFada.Multiplicativo.Codigo()).ComBaseLegal(BaseLegalId).Construir();

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDoFada.CamposObrigatorios");
    }

    [Fact(DisplayName = "Handle com RegraCodigo informado mas sem BaseLegalId recusa, sem consultar o reader")]
    public async Task Handle_SemBaseLegalId_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).QueAplica().ComRegra(RegraDoFada.Multiplicativo.Codigo()).ComFator(1.20m).Construir();

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDoFada.CamposObrigatorios");
        await mocks.BaseLegalDoFadaReader.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com regra inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).QueAplica().ComRegra("REGRA_INEXISTENTE_DE_TESTE", "versao-inexistente-de-teste").ComFator(1.20m).ComBaseLegal(BaseLegalId).Construir();

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDoFada.RegraNaoEncontrada");
        result.Error.Message.Should().NotContain("REGRA_INEXISTENTE_DE_TESTE").And.NotContain("versao-inexistente-de-teste");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de REGRA_FADA recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraDoFada.Multiplicativo.Codigo(), "versao-de-tipo-invalido-de-teste", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraDoFada.Multiplicativo.Codigo(), TipoRegra.RegraCalculo));

        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).QueAplica().ComRegra(RegraDoFada.Multiplicativo.Codigo(), "versao-de-tipo-invalido-de-teste").ComFator(1.20m).ComBaseLegal(BaseLegalId).Construir();

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDoFada.RegraTipoInvalido");
        result.Error.Message.Should().NotContain(RegraDoFada.Multiplicativo.Codigo()).And.NotContain("versao-de-tipo-invalido-de-teste");
    }

    [Fact(DisplayName = "Handle com Base Legal inexistente ou desativada recusa (CA-02) sem persistir")]
    public async Task Handle_BaseLegalNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        ComRegraDoFadaNoCatalogo(mocks);
        Guid idInexistente = Guid.CreateVersion7();
        mocks.BaseLegalDoFadaReader.ObterPorIdAsync(idInexistente, Arg.Any<CancellationToken>())
            .Returns((BaseLegalDoFadaView?)null);
        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).QueAplica().ComRegra(RegraDoFada.Multiplicativo.Codigo()).ComFator(1.20m).ComBaseLegal(idInexistente).Construir();

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        ExigirRecusaDaBaseLegalNaoEncontrada(result);
        processo.Fada.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com regra e base legal inexistentes recusa as duas juntas, sem persistir")]
    public async Task Handle_RegraEBaseLegalNaoEncontradas_SomaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);
        Guid idInexistente = Guid.CreateVersion7();
        mocks.BaseLegalDoFadaReader.ObterPorIdAsync(idInexistente, Arg.Any<CancellationToken>()).Returns((BaseLegalDoFadaView?)null);
        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).QueAplica().ComRegra("REGRA_INEXISTENTE_DE_TESTE", "versao-inexistente-de-teste").ComFator(1.20m).ComBaseLegal(idInexistente).Construir();

        Result result = await Handle(command, mocks);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            (null, "ConfiguracaoDoFada.RegraNaoEncontrada"),
            ("baseLegal.id", "ConfiguracaoDoFada.BaseLegalNaoEncontrada"));
        processo.Fada.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static void ExigirRecusaDaBaseLegalNaoEncontrada(Result result)
    {
        FieldError recusa = result.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("baseLegal.id");
        recusa.Error.Code.Should().Be("ConfiguracaoDoFada.BaseLegalNaoEncontrada");
        recusa.Error.Message.Should().Be("A base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) deve ser uma das bases legais do cadastro.");
    }

    [Fact(DisplayName =
        "Handle com processo já publicado propaga MutacaoPosPublicacaoBloqueada e NÃO persiste (CA-04)")]
    public async Task Handle_ProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraDoFada.Multiplicativo.Codigo(), "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraDoFada.Multiplicativo.Codigo(), TipoRegra.RegraFada));

        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).QueAplica().ComRegra(RegraDoFada.Multiplicativo.Codigo()).ComFator(1.20m).ComBaseLegal(BaseLegalId).Construir();

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.Fada.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName =
        "Handle de remoção (RegraCodigo nulo) com processo já publicado propaga bloqueio e NÃO persiste (CA-04)")]
    public async Task Handle_RemocaoComProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFadaCommand command = FadaDeTeste.Para(processo.Id).Construir();

        Result result = await Handle(command, mocks);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
