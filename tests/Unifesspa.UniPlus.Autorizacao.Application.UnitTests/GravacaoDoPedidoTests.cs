namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

public sealed class GravacaoDoPedidoTests
{
    [Fact(DisplayName = "Sem conflito, grava uma vez e não descarta nada")]
    public async Task Gravar_SemConflito_Grava()
    {
        UnidadeDeTrabalho unidade = new();

        Result resultado = await GravacaoDoPedido.GravarAsync(unidade, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        unidade.Gravacoes.Should().Be(1);
        unidade.Descartou.Should().BeFalse();
    }

    [Fact(DisplayName = "O conflito de concorrência descarta o rastreamento e devolve o conflito do pedido")]
    public async Task Gravar_ComConflito_DescartaEDevolveOConflito()
    {
        UnidadeDeTrabalho unidade = UnidadeDeTrabalho.ComConflito();

        Result resultado = await GravacaoDoPedido.GravarAsync(unidade, CancellationToken.None);

        resultado.Error!.Code.Should().Be(PedidoErrorCodes.ConflitoDeConcorrencia);
        unidade.Descartou.Should().BeTrue();
    }

    [Fact(DisplayName = "A falha que não é conflito propaga sem descartar")]
    public async Task Gravar_FalhaDesconhecida_Propaga()
    {
        UnidadeDeTrabalho unidade = new(new InvalidOperationException("banco fora"));

        Func<Task> gravar = () => GravacaoDoPedido.GravarAsync(unidade, CancellationToken.None);

        await gravar.Should().ThrowAsync<InvalidOperationException>();
        unidade.Descartou.Should().BeFalse();
    }
}
