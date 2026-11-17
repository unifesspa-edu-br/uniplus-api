namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

public sealed class CancelamentoDoPedidoTests
{
    [Fact(DisplayName = "O solicitante cancela o pedido, que se grava uma vez")]
    public async Task Cancelar_DoSolicitante_CancelaEGrava()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        UnidadeDeTrabalho unidade = new();

        (Result resposta, _) = await Cancelamento(pedido, unidade)
            .CancelarAsync(pedido.Id, ApoioDoPedido.Betina.Identidade, CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue();
        pedido.Andamento.Situacao.Should().Be(SituacaoDoPedido.Cancelado);
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "O pedido de outra pessoa não se distingue do inexistente")]
    public async Task Cancelar_DeOutraPessoa_NaoEncontrado()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        UnidadeDeTrabalho unidade = new();

        (Result resposta, _) = await Cancelamento(pedido, unidade)
            .CancelarAsync(pedido.Id, ApoioDoPedido.Leandro.Identidade, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
        pedido.Andamento.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "O pedido inexistente não é encontrado")]
    public async Task Cancelar_Inexistente_NaoEncontrado()
    {
        (Result resposta, _) = await Cancelamento(null, new UnidadeDeTrabalho())
            .CancelarAsync(Guid.NewGuid(), ApoioDoPedido.Betina.Identidade, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "O pedido que já não se cancela descarta o rastreamento e devolve a recusa do pedido")]
    public async Task Cancelar_SituacaoInvalida_DescartaSemGravar()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        pedido.Cancelar(ApoioDoPedido.Agora).IsSuccess.Should().BeTrue();
        UnidadeDeTrabalho unidade = new();

        (Result resposta, _) = await Cancelamento(pedido, unidade)
            .CancelarAsync(pedido.Id, ApoioDoPedido.Betina.Identidade, CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.SituacaoInvalida);
        unidade.Descartou.Should().BeTrue();
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "O conflito de concorrência propaga: o cancelamento não leva Idempotency-Key")]
    public async Task Cancelar_ComConflito_Propaga()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        CancelamentoDoPedido cancelamento = Cancelamento(pedido, UnidadeDeTrabalho.ComConflito());

        Func<Task> cancelar = () => cancelamento.CancelarAsync(pedido.Id, ApoioDoPedido.Betina.Identidade, CancellationToken.None);

        await cancelar.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact(DisplayName = "Sem identidade no token, o cancelamento recusa o autor não identificado")]
    public async Task Handle_SemIdentidade_AutorNaoIdentificado()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();

        (Result resposta, _) = await CancelarPedidoDeConcessaoCommandHandler.Handle(
            new CancelarPedidoDeConcessaoCommand(pedido.Id),
            Cancelamento(pedido, new UnidadeDeTrabalho()),
            new DoublesDePerfis.Usuario(null),
            CancellationToken.None);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.AutorNaoIdentificado);
    }

    private static CancelamentoDoPedido Cancelamento(PedidoDeConcessao? pedido, UnidadeDeTrabalho unidade) =>
        new(new PedidosNaMemoria(pedido), new DoublesDeAvisos.PerfilQualquer(), DoublesDeAvisos.Avisos(), unidade, TimeProvider.System);
}
