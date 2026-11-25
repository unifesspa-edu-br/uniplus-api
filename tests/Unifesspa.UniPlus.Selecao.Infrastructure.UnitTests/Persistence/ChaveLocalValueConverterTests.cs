namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;

public sealed class ChaveLocalValueConverterTests
{
    [Fact(DisplayName = "Uma chave válida é gravada e lida sem alterações")]
    public void Conversao_ChaveValida_PreservaAChave()
    {
        ChaveLocalValueConverter conversor = new();
        ChaveLocal chave = ChaveLocal.Criar("fase_selecao").Value;

        string gravada = conversor.ConvertToProviderExpression.Compile()(chave);
        ChaveLocal lida = conversor.ConvertFromProviderExpression.Compile()(gravada);

        gravada.Should().Be("fase_selecao");
        lida.Should().Be(chave);
    }

    [Fact(DisplayName = "Uma chave inválida no banco causa erro sem expor o valor lido")]
    public void Conversao_ChaveInvalida_LancaErroSemExporOValor()
    {
        const string valor = "chave inválida";
        ChaveLocalValueConverter conversor = new();
        Action leitura = () => conversor.ConvertFromProviderExpression.Compile()(valor);

        leitura.Should().Throw<InvalidOperationException>()
            .WithMessage("Dado inválido no banco ao reidratar ChaveLocal: ChaveLocal.Invalida. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.")
            .Which.Message.Should().NotContain(valor);
    }
}
