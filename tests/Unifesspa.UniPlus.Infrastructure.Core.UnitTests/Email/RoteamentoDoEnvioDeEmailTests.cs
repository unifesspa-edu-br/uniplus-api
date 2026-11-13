namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Email;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Email;

public sealed class RoteamentoDoEnvioDeEmailTests
{
    [Fact(DisplayName = "Sem teto configurado, a fila de e-mail entrega até cinco mensagens ao mesmo tempo")]
    public void EnviosSimultaneos_SemConfiguracao_UsaOPadrao()
    {
        EnvioDeEmailServiceCollectionExtensions.EnviosSimultaneos(Configuracao(null)).Should().Be(5);
    }

    [Fact(DisplayName = "O teto configurado chega ao listener da fila de e-mail")]
    public void EnviosSimultaneos_Configurado_UsaOValor()
    {
        EnvioDeEmailServiceCollectionExtensions.EnviosSimultaneos(Configuracao("3")).Should().Be(3);
    }

    [Theory(DisplayName = "Teto inválido impede a subida antes de chegar ao Wolverine")]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("21")]
    public void EnviosSimultaneos_Invalido_Falha(string teto)
    {
        Action leitura = () => EnvioDeEmailServiceCollectionExtensions.EnviosSimultaneos(Configuracao(teto));

        leitura.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("EnviosSimultaneos");
    }

    private static IConfiguration Configuracao(string? teto) => new ConfigurationBuilder()
        .AddInMemoryCollection(teto is null ? [] : [new($"{EmailSmtpOptions.SectionName}:EnviosSimultaneos", teto)])
        .Build();
}
