namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class CryptographyDiTests
{
    private static IConfiguration CriarConfig(string provider, string? localKey = null)
    {
        Dictionary<string, string?> values = new()
        {
            ["UniPlus:Encryption:Provider"] = provider,
        };

        if (localKey is not null)
        {
            values["UniPlus:Encryption:LocalKey"] = localKey;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderLocal_DeveResolverLocalAesEncryptionService()
    {
        string chaveValida = Convert.ToBase64String(new byte[32]);
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("local", chaveValida))
            .BuildServiceProvider();

        IUniPlusEncryptionService servico = sp.GetRequiredService<IUniPlusEncryptionService>();

        servico.Should().BeOfType<LocalAesEncryptionService>();
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderInvalido_DeveLancarInvalidOperationExceptionAoResolver()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("desconhecido"))
            .BuildServiceProvider();

        Action ato = () => sp.GetRequiredService<IUniPlusEncryptionService>();

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*'desconhecido'*");
    }
}
