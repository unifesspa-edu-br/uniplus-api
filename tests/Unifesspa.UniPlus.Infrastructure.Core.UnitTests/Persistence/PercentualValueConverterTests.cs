namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class PercentualValueConverterTests
{
    private readonly PercentualValueConverter _conversor = new();

    [Fact(DisplayName = "A leitura devolve o percentual que foi gravado")]
    public void ConvertFromProvider_PercentualGravado_DevolveOMesmoValor()
    {
        Percentual original = Percentual.Criar(12.34m).Value ?? throw new InvalidOperationException();

        object? lido = _conversor.ConvertFromProvider(_conversor.ConvertToProvider(original));

        lido.Should().Be(original);
    }

    [Fact(DisplayName = "A leitura de percentual fora da faixa informa o valor e o código do erro")]
    public void ConvertFromProvider_PercentualForaDaFaixa_Lanca() =>
        FluentActions.Invoking(() => _conversor.ConvertFromProvider(100.01m))
            .Should().Throw<InvalidOperationException>().Which.Message.Should().Be(
                "Dado inválido no banco ao reidratar Percentual: Percentual.ForaDeFaixa. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
}
