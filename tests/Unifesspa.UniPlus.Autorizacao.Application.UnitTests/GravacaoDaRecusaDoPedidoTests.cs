namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.ApoioDaRecusa;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>A gravação da recusa de uma etapa do pedido: o que se grava, o que se descarta e o que se avisa.</summary>
public sealed class GravacaoDaRecusaDoPedidoTests
{
    [Fact(DisplayName = "A recusa aceita encerra o pedido, grava uma vez e responde com o pedido recusado")]
    public async Task GravarRecusa_Aceita_GravaEResponde()
    {
        DecisaoDaEtapa decisao = Decisao();
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, _) = await Recusar(decisao, ApoioDoPedido.Diogo.Identidade, unidade);

        resposta.IsSuccess.Should().BeTrue();
        resposta.Value!.Situacao.Should().Be(SituacaoDoPedidoCodigo.Recusado);
        unidade.Gravacoes.Should().Be(1);
        unidade.Descartou.Should().BeFalse();
    }

    [Fact(DisplayName = "A recusa do agregado descarta o rastreamento, não grava e devolve as violações")]
    public async Task GravarRecusa_RecusadaPeloAgregado_DescartaSemGravar()
    {
        DecisaoDaEtapa decisao = Decisao();
        UnidadeDeTrabalho unidade = new();

        (Result<PedidoDeConcessaoDto> resposta, IEnumerable<object> mensagens) = await Recusar(decisao, ApoioDoPedido.Betina.Identidade, unidade);

        resposta.IsFailure.Should().BeTrue();
        decisao.Pedido.Andamento.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        unidade.Descartou.Should().BeTrue();
        unidade.Gravacoes.Should().Be(0);
        mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "O conflito de concorrência devolve a recusa do pedido e descarta o rastreamento")]
    public async Task GravarRecusa_ComConflito_DevolveOConflito()
    {
        DecisaoDaEtapa decisao = Decisao();
        UnidadeDeTrabalho unidade = UnidadeDeTrabalho.ComConflito();

        (Result<PedidoDeConcessaoDto> resposta, IEnumerable<object> mensagens) = await Recusar(decisao, ApoioDoPedido.Diogo.Identidade, unidade);

        resposta.Error!.Code.Should().Be(PedidoErrorCodes.ConflitoDeConcorrencia);
        unidade.Descartou.Should().BeTrue();
        mensagens.Should().BeEmpty();
    }

    private static Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> Recusar(
        DecisaoDaEtapa decisao, Identidade decisor, UnidadeDeTrabalho unidade)
    {
        SituacaoDoPedido situacaoAnterior = decisao.Pedido.Andamento.Situacao;
        Result recusa = decisao.Pedido.Recusar(decisor, "Não atende o perfil.", decisao.Avaliado, ApoioDoPedido.Agora);

        return Gravacao(unidade).GravarRecusaAsync(decisao, recusa, situacaoAnterior, CancellationToken.None);
    }
}
