namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaCascataDeRemanejamento;

public sealed class DefinirCascataRemanejamentoPersistenciaCommandHandlerTests
{
    [Fact(DisplayName = "Handle com regra válida e matriz batendo célula a célula define a cascata e persiste")]
    public async Task Handle_RegraValidaEMatrizBatendo_DefineEPersiste()
    {
        (ProcessoSeletivo processo, Mocks mocks) = ProcessoComRegraCascataNoCatalogo();
        DestinoRemanejamentoInput[] destinos = [new("LB_PPI", 1, "LB_Q"), new("LB_PPI", 2, "LB_PCD")];

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            ComandoComDestinos(processo, "AC", destinos), mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Cascata!.FallbackCodigo.Should().Be("AC");
        processo.Cascata.Destinos.Should().HaveCount(2);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ADR-0125: Handle acumula violações de itens distintos com o índice do item prefixado ao field")]
    public async Task Handle_DoisItensInvalidos_AcumulaComIndicePrefixadoAoField()
    {
        (ProcessoSeletivo processo, Mocks mocks) = ProcessoComRegraCascataNoCatalogo();
        DestinoRemanejamentoInput[] destinos = [new("LB_PPI", 1, "LB_Q"), new("lb-ppi", 0, "LB_PCD")];

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            ComandoComDestinos(processo, "AC", destinos), mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["destinos[1].modalidadeOrigemCodigo", "destinos[1].ordem"]);
    }

    [Fact(DisplayName = "ADR-0125: Handle acumula fallback inválido junto com item malformado, mesmo sem checagem de coerência entre itens")]
    public async Task Handle_FallbackInvalidoEItemMalformado_AcumulaOsDois()
    {
        (ProcessoSeletivo processo, Mocks mocks) = ProcessoComRegraCascataNoCatalogo();

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            ComandoComDestinos(processo, "AC-1", [new("LB_PPI", 0, "LB_Q")]), mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
            ["ConfiguracaoCascataRemanejamento.FallbackObrigatorio", "ConfiguracaoCascataRemanejamento.OrdemInvalida"]);
    }

    [Fact(DisplayName = "ADR-0125: validação de forma precede a consulta ao catálogo de regras, mesmo com regra inexistente")]
    public async Task Handle_FallbackInvalidoComRegraInexistente_ValidacaoVenceAConsultaAoCatalogo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirCascataRemanejamentoCommand command = new(processo.Id, "INEXISTENTE", "v1", "ac-invalido", [new("LB_PPI", 1, "LB_Q")]);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.FallbackObrigatorio");
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Handle com processo já publicado propaga bloqueio e NÃO persiste (CA-07)")]
    public async Task Handle_ProcessoPublicado_PropagaBloqueioENaoPersiste()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            ComandoComDestinos(processo, "AC", [new("LB_PPI", 1, "LB_Q")]), mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.Cascata.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static DefinirCascataRemanejamentoCommand ComandoComDestinos(
        ProcessoSeletivo processo, string fallback, IReadOnlyList<DestinoRemanejamentoInput> destinos) =>
        new(processo.Id, CriterioDeRemanejamento.OrdemDaLei12711.Codigo(), "v1", fallback, destinos);
}
