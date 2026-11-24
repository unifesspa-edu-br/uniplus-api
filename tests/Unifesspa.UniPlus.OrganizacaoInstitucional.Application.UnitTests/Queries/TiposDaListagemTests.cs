namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

public sealed class TiposDaListagemTests
{
    private const int Centro = (int)TipoUnidade.Centro;
    private const int Instituto = (int)TipoUnidade.Instituto;

    [Fact(DisplayName = "Sem valores de tipo (nulo ou vazio), a listagem não filtra por tipo")]
    public void SemValores_NaoFiltra()
    {
        Result<IReadOnlyList<TipoUnidade>> nulo = TiposDaListagem.Resolver(null);
        Result<IReadOnlyList<TipoUnidade>> vazio = TiposDaListagem.Resolver([]);

        nulo.Value.Should().BeEmpty();
        vazio.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "Valores repetidos entram uma vez, na ordem em que foram pedidos")]
    public void Repetidos_EntramUmaVezNaOrdemPedida()
    {
        Result<IReadOnlyList<TipoUnidade>> resultado = TiposDaListagem.Resolver([Instituto, Centro, Instituto, Centro]);

        resultado.Value.Should().Equal(TipoUnidade.Instituto, TipoUnidade.Centro);
    }

    [Fact(DisplayName = "Todo tipo definido é aceito")]
    public void TodoTipoDefinido_EhAceito()
    {
        int[] definidos = [.. Enum.GetValues<TipoUnidade>().Select(tipo => (int)tipo)];

        Result<IReadOnlyList<TipoUnidade>> resultado = TiposDaListagem.Resolver(definidos);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().HaveCount(definidos.Length);
    }

    [Theory(DisplayName = "O número que não é tipo de Unidade é recusado")]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(-1)]
    public void ValorQueNaoEhTipo_EhRecusado(int valor)
    {
        Result<IReadOnlyList<TipoUnidade>> resultado = TiposDaListagem.Resolver([valor]);

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.FiltroDeTipoInvalido);
        resultado.Error.Message.Should().Contain($"'{valor}'");
    }

    [Fact(DisplayName = "O valor inválido no meio recusa o pedido inteiro e a mensagem cita o primeiro inválido")]
    public void InvalidoNoMeio_RecusaOPedidoCitandoOPrimeiroInvalido()
    {
        Result<IReadOnlyList<TipoUnidade>> resultado = TiposDaListagem.Resolver([Centro, 999, Instituto, -1]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().Contain("'999'").And.NotContain("-1");
    }
}
