namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

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
        IProcessoSeletivoRepository Repository, IRegraCatalogoReader RegraCatalogoReader, ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<IRegraCatalogoReader>(), Substitute.For<ISelecaoUnitOfWork>());
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
            Guid.CreateVersion7(), "X", "v1", null, null, null, "Y", "v1", 1, []);

        Result result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com FORMULA-MEDIA-PONDERADA + arredondamento resolve e persiste")]
    public async Task Handle_MediaPonderadaComArredondamento_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.FormulaMediaPonderada, "v1",
            RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1,
            []);

        Result result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao.Should().NotBeNull();
        processo.Classificacao!.RegraArredondamento.Should().NotBeNull();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com CLASSIFICACAO-IMPORTADA sem arredondamento resolve e persiste (INV-B8)")]
    public async Task Handle_Importada_SemArredondamento_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("SiSU 2026", TipoProcesso.SiSU);
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
            []);

        Result result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao!.RegraArredondamento.Should().BeNull();
    }

    [Fact(DisplayName = "Handle com ELIM-NOTA-MINIMA-ETAPA resolve args e persiste")]
    public async Task Handle_ComEliminacaoNotaMinimaEtapa_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Convênios 2026", TipoProcesso.PSIQ);
        EtapaProcesso etapa = EtapaProcesso.Criar("Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa]);

        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.RegraCatalogoReader.ObterAsync(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, TipoRegra.RegraEliminacao));

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.FormulaMediaPonderada, "v1",
            RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1,
            [new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", etapa.Id, 4m, null)]);

        Result result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        RegraEliminacao eliminacao = processo.Classificacao!.RegrasEliminacao.Single();
        ((ArgsElimNotaMinimaEtapa)eliminacao.Args).EtapaRef.Should().Be(etapa.Id);
    }

    [Fact(DisplayName = "Handle com ELIM-NOTA-MINIMA-ETAPA sem EtapaRef/NotaMinima recusa")]
    public async Task Handle_EliminacaoSemEtapaRef_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        Mocks mocks = NovosMocks(processo, processo.Id);
        MockRegrasBasicas(mocks);
        mocks.RegraCatalogoReader.ObterAsync(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, TipoRegra.RegraEliminacao));

        DefinirClassificacaoCommand command = new(
            processo.Id,
            RegraCalculoCodigo.FormulaMediaPonderada, "v1",
            RegraArredondamentoCodigo.PrecisaoTruncar, "v1", 2,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1,
            [new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", null, null, null)]);

        Result result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.EtapaRefENotaMinimaObrigatorios");
    }

    [Fact(DisplayName = "Handle com regra de cálculo inexistente recusa")]
    public async Task Handle_RegraCalculoNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirClassificacaoCommand command = new(
            processo.Id, "INEXISTENTE", "v1", null, null, null, RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1, []);

        Result result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoClassificacao.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de cálculo de tipo inválido recusa")]
    public async Task Handle_RegraCalculoTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraCalculoCodigo.FormulaMediaPonderada, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraCalculoCodigo.FormulaMediaPonderada, TipoRegra.RegraArredondamento));

        DefinirClassificacaoCommand command = new(
            processo.Id, RegraCalculoCodigo.FormulaMediaPonderada, "v1", null, null, null,
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", 1, []);

        Result result = await DefinirClassificacaoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoClassificacao.RegraTipoInvalido");
    }
}
