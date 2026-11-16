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

public sealed class DefinirCascataRemanejamentoEsquemaArgsCommandHandlerTests
{
    [Fact(DisplayName = "Handle com fallback divergente do esquema_args recusa com MatrizDivergenteDaRegra, mesmo com forma válida")]
    public async Task Handle_FallbackDivergenteDoEsquemaArgs_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        // Forma válida (a forma da cascata de remanejamento de cotas, UNI-REQ-0056, passa), mas o fallback ("AMPLA", não "AC") diverge
        // do esquema_args congelado da regra.
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AMPLA",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("LB_PPI", 2, "LB_PCD")],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com destino trocado em relação ao esquema_args recusa com MatrizDivergenteDaRegra")]
    public async Task Handle_DestinoTrocadoNoEsquemaArgs_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        // O esquema_args de RegraCascataValida() é LB_PPI → [LB_Q, LB_PCD]; aqui a
        // ordem 2 aponta para LB_EP em vez de LB_PCD — um destino trocado.
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("LB_PPI", 2, "LB_EP")],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com linha de origem omitida em relação ao esquema_args recusa com MatrizDivergenteDaRegra")]
    public async Task Handle_OrigemOmitidaNoEsquemaArgs_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        RegraCatalogo regraComDuasOrigens = RegraCatalogo.Criar(
            RegraRemanejamentoCodigo.Cascata, "v1", TipoRegra.CriterioRemanejamento,
            Json("""{"fallbackCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":["LB_Q"]},{"origem":"LB_Q","destinos":["LB_PPI"]}]}"""),
            Json("[]"), "ADR-0120");
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(regraComDuasOrigens);

        // O payload aplicado só declara LB_PPI — omite a linha de LB_Q que o
        // esquema_args da regra também exige.
        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com esquema_args repetindo uma origem não deixa outra origem sem conferir")]
    public async Task Handle_EsquemaArgsComOrigemRepetida_NaoCobreOrigemNaoConferida()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        // O esquema_args declara LB_PPI DUAS vezes e nunca declara LB_Q — se a comparação
        // fosse por CONTAGEM de linhas em vez de pelo CONJUNTO de origens conferidas, as duas
        // entradas de LB_PPI bateriam com as duas origens do payload (LB_PPI e LB_Q), e o
        // destino arbitrário de LB_Q no payload passaria sem nunca ser comparado a nada.
        RegraCatalogo regraComOrigemRepetida = RegraCatalogo.Criar(
            RegraRemanejamentoCodigo.Cascata, "v1", TipoRegra.CriterioRemanejamento,
            Json("""{"fallbackCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":["LB_Q"]},{"origem":"LB_PPI","destinos":["LB_Q"]}]}"""),
            Json("[]"), "ADR-0120");
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(regraComOrigemRepetida);

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [
                new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"),
                new DestinoRemanejamentoInput("LB_Q", 1, "DESTINO_NUNCA_CONFERIDO"),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue("LB_Q nunca foi comparado contra o esquema_args — a origem repetida não pode cobri-la");
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com item nulo na lista de destinos recusa com CamposObrigatorios, sem lançar")]
    public async Task Handle_ItemNuloEmDestinos_RecusaSemLancar()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.RegraCatalogoReader.ObterAsync(RegraRemanejamentoCodigo.Cascata, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCascataValida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [null!], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.CamposObrigatorios");
    }
}
