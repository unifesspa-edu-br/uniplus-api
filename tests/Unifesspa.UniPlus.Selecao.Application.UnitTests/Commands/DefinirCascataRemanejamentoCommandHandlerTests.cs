namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaCascataDeRemanejamento;

/// <summary>
/// Cobertura do <see cref="DefinirCascataRemanejamentoCommandHandler"/>
/// (cascata de remanejamento de cotas, UNI-REQ-0056, Story #575): remoção por ausência total, recusa de
/// presença parcial, resolução da regra no catálogo, e a ordem
/// forma-antes-de-conteúdo — a factory (cascata de remanejamento de cotas, UNI-REQ-0056) roda antes da
/// comparação célula a célula contra o <c>declaracao_da_regra</c> (cascata de remanejamento de cotas, UNI-REQ-0056),
/// para que um erro de forma nunca seja mascarado por
/// <c>MatrizDivergenteDaRegra</c>.
/// </summary>
public sealed class DefinirCascataRemanejamentoCommandHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirCascataRemanejamentoCommand command = new(Guid.CreateVersion7(), null, null, null, null);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com os quatro campos nulos remove a cascata, sem consultar o catálogo de regras")]
    public async Task Handle_TodosOsCamposNulos_Remove()
    {
        ProcessoSeletivo processo = NovoProcessoComCascata();

        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirCascataRemanejamentoCommand command = new(processo.Id, null, null, null, null);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.Cascata.Should().BeNull();
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Handle com combinação parcial dos quatro campos recusa com CamposObrigatorios")]
    [MemberData(nameof(ApoioDaCascataDeRemanejamento.CombinacoesParciais), MemberType = typeof(ApoioDaCascataDeRemanejamento))]
    public async Task Handle_CombinacaoParcial_Recusa(
        string? regraCodigo, string? regraVersao, string? fallbackCodigo, IReadOnlyList<DestinoRemanejamentoInput>? destinos)
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, regraCodigo, regraVersao, fallbackCodigo, destinos);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.CamposObrigatorios");
    }

    [Fact(DisplayName = "Handle com regra inexistente no catálogo recusa com RegraNaoEncontrada")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, "INEXISTENTE", "v1", "AC", [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")]);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.RegraNaoEncontrada");
        result.Error.Message.Should().NotContain(command.RegraCodigo!);
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de criterio_remanejamento recusa com RegraTipoInvalido")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        RegraCatalogo regraTipoErrado = RegraCatalogo.Criar(
            RegraRemanejamentoCodigo.Cascata, "v1", TipoRegra.RegraCalculo, Json("{}"), Json("[]"), "base legal");
        ConfigurarRegra(mocks, regraTipoErrado);
        DefinirCascataRemanejamentoCommand command = Comando(processo.Id);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.RegraTipoInvalido");
        result.Error.Message.Should().NotContain(command.RegraCodigo!);
    }

    [Fact(DisplayName = "Handle com ordem inválida (cascata de remanejamento de cotas, UNI-REQ-0056) recusa pela factory, ANTES de comparar contra o declaracao_da_regra")]
    public async Task Handle_OrdemInvalida_RecusaPelaFactoryAntesDoDeclaracaoDaRegra()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        ConfigurarRegra(mocks, RegraCascataValida());
        DefinirCascataRemanejamentoCommand command = Comando(processo.Id, ordem: 0);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.OrdemInvalida");
    }
}
