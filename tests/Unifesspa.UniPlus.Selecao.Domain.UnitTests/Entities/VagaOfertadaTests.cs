namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class VagaOfertadaTests
{
    [Fact(DisplayName = "A quantidade negativa vinda do cálculo é defeito de programa e lança")]
    public void Criar_QuantidadeNegativa_Lanca()
    {
        Action criar = () => VagaOfertada.Criar(Guid.CreateVersion7(), "IND", -1);

        criar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory(DisplayName = "A quantidade zero ou positiva cria a linha do quadro")]
    [InlineData(0)]
    [InlineData(7)]
    public void Criar_QuantidadeNaoNegativa_Cria(int quantidade)
    {
        Result<VagaOfertada> resultado = VagaOfertada.Criar(Guid.CreateVersion7(), " IND ", quantidade);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Quantidade.Should().Be(quantidade);
        resultado.Value.ModalidadeCodigo.Should().Be("IND");
    }

    [Fact(DisplayName = "O código da modalidade em branco é recusado")]
    public void Criar_CodigoEmBranco_Recusa() =>
        VagaOfertada.Criar(Guid.CreateVersion7(), " ", 1).IsFailure.Should().BeTrue();
}
