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

public sealed class DefinirBonusRegionalCommandHandlerTests
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

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirBonusRegionalCommand command = new(Guid.CreateVersion7(), null, null, null, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com RegraCodigo nulo remove o bônus e persiste (toggle por ausência, RN05)")]
    public async Task Handle_RegraCodigoNulo_RemoveBonus()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU);
        processo.DefinirBonusRegional(ConfiguracaoBonusRegional.Criar(
            Domain.ValueObjects.ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)).Value!,
            1.20m, null, null, null).Value!);

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirBonusRegionalCommand command = new(processo.Id, null, null, null, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.BonusRegional.Should().BeNull();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com regra válida define o bônus e persiste")]
    public async Task Handle_RegraValida_DefineBonus()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Convênios 2026", TipoProcesso.PSVR);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraBonus));

        DefinirBonusRegionalCommand command = new(processo.Id, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, "Marabá", "RN05");

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.BonusRegional.Should().NotBeNull();
        processo.BonusRegional!.Fator.Should().Be(1.20m);
        processo.BonusRegional.MunicipioConvenio.Should().Be("Marabá");
    }

    [Fact(DisplayName = "Handle com RegraCodigo informado mas sem Fator recusa")]
    public async Task Handle_SemFator_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU);
        Mocks mocks = NovosMocks(processo, processo.Id);

        DefinirBonusRegionalCommand command = new(processo.Id, RegraBonusCodigo.Multiplicativo, "v1", null, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.CamposObrigatorios");
    }

    [Fact(DisplayName = "Handle com regra inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirBonusRegionalCommand command = new(processo.Id, "INEXISTENTE", "v1", 1.20m, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de regra_bonus recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026", TipoProcesso.SiSU);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraBonusCodigo.Multiplicativo, "v1", Arg.Any<CancellationToken>())
            .Returns(Regra(RegraBonusCodigo.Multiplicativo, TipoRegra.RegraCalculo));

        DefinirBonusRegionalCommand command = new(processo.Id, RegraBonusCodigo.Multiplicativo, "v1", 1.20m, null, null, null);

        Result result = await DefinirBonusRegionalCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoBonusRegional.RegraTipoInvalido");
    }
}
