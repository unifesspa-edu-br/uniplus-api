namespace Unifesspa.UniPlus.Application.Abstractions.UnitTests.Consultas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class FiltroPorCodigoPedidoTests
{
    private static readonly DomainError Recusa = new("Filtro.Invalido", "Código de filtro inválido.");

    [Fact]
    public void Ler_CodigoExato_DevolveOValor()
    {
        Result<Cor?> resultado = FiltroPorCodigoPedido.Ler<Cor>("AZUL", Recusa);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(Cor.Azul);
    }

    [Fact]
    public void Ler_CodigoEmMinusculas_Recusa() =>
        FiltroPorCodigoPedido.Ler<Cor>("azul", Recusa).IsFailure.Should().BeTrue();

    [Theory]
    [InlineData(" AZUL")]
    [InlineData("AZUL ")]
    public void Ler_CodigoComEspacoEmVolta_Recusa(string codigo) =>
        FiltroPorCodigoPedido.Ler<Cor>(codigo, Recusa).IsFailure.Should().BeTrue();

    [Fact]
    public void Ler_TextoSomenteComEspacos_Recusa() =>
        FiltroPorCodigoPedido.Ler<Cor>("   ", Recusa).IsFailure.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ler_CodigoAusente_DevolveSucessoSemFiltro(string? codigo)
    {
        Result<Cor?> resultado = FiltroPorCodigoPedido.Ler<Cor>(codigo, Recusa);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact]
    public void Ler_CodigoDesconhecido_DevolveARecusaRecebida()
    {
        Result<Cor?> resultado = FiltroPorCodigoPedido.Ler<Cor>("VERDE", Recusa);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().BeSameAs(Recusa);
    }

    private enum Cor
    {
        [CodigoDeContrato("AZUL", "Azul")]
        Azul,
    }
}
