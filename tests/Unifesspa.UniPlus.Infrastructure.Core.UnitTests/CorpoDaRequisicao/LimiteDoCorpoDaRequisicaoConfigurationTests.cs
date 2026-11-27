namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.CorpoDaRequisicao;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.CorpoDaRequisicao;

public sealed class LimiteDoCorpoDaRequisicaoConfigurationTests
{
    [Fact(DisplayName = "Sem configuração, o limite é o padrão de 2 MiB")]
    public void SemConfiguracao_UsaOPadraoDe2MiB()
    {
        LimiteDoCorpoDaRequisicaoOptions opcoes = Resolver(new Dictionary<string, string?>());

        opcoes.TamanhoMaximoEmBytes.Should().Be(2 * 1024 * 1024);
        LimiteDoCorpoDaRequisicaoOptions.PadraoEmBytes.Should().Be(2_097_152);
    }

    [Fact(DisplayName = "O valor da seção de configuração vale no lugar do padrão")]
    public void ComValorNaSecao_LeOValorDaSecao()
    {
        LimiteDoCorpoDaRequisicaoOptions opcoes = Resolver(new Dictionary<string, string?>
        {
            ["UniPlus:LimiteDoCorpoDaRequisicao:TamanhoMaximoEmBytes"] = "4096",
        });

        opcoes.TamanhoMaximoEmBytes.Should().Be(4096);
    }

    [Theory(DisplayName = "Limite zero ou negativo derruba a subida")]
    [InlineData("0")]
    [InlineData("-1")]
    public void LimiteQueNaoEPositivo_DerrubaASubida(string valor)
    {
        Action resolver = () => Resolver(new Dictionary<string, string?>
        {
            ["UniPlus:LimiteDoCorpoDaRequisicao:TamanhoMaximoEmBytes"] = valor,
        });

        resolver.Should().Throw<OptionsValidationException>().WithMessage("*TamanhoMaximoEmBytes*positivo*");
    }

    private static LimiteDoCorpoDaRequisicaoOptions Resolver(Dictionary<string, string?> valores)
    {
        IConfiguration configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        using ServiceProvider provedor = new ServiceCollection()
            .AddLimiteDoCorpoDaRequisicao(configuracao)
            .BuildServiceProvider();
        return provedor.GetRequiredService<IOptions<LimiteDoCorpoDaRequisicaoOptions>>().Value;
    }
}
