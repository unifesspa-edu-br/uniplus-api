namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using System.Security.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>
/// O provedorDeCifra configurado decide, na primeira resolução, qual implementação atende a cifra e o
/// índice cego; os dois serviços seguem sempre o mesmo provedorDeCifra.
/// </summary>
public sealed class ProvedorDeCifraNoRegistroTests
{
    [Theory]
    [InlineData("local")]
    [InlineData("LOCAL")]
    public void IUniPlusEncryptionService_ProvedorLocal_ResolveCifraLocal(string provedorDeCifra)
    {
        using ServiceProvider provedorDeServicos = CriarProvedor(provedorDeCifra);

        provedorDeServicos.GetRequiredService<IUniPlusEncryptionService>().Should().BeOfType<LocalAesEncryptionService>();
    }

    [Theory]
    [InlineData("vault")]
    [InlineData("VAULT")]
    public void IUniPlusEncryptionService_ProvedorVault_ResolveCifraDoVault(string provedorDeCifra)
    {
        using ServiceProvider provedorDeServicos = CriarProvedor(provedorDeCifra);

        provedorDeServicos.GetRequiredService<IUniPlusEncryptionService>().Should().BeOfType<VaultTransitEncryptionService>();
    }

    [Theory]
    [InlineData("local")]
    [InlineData("LOCAL")]
    public void IUniPlusBlindIndexService_ProvedorLocal_ResolveIndiceCegoLocal(string provedorDeCifra)
    {
        using ServiceProvider provedorDeServicos = CriarProvedor(provedorDeCifra);

        provedorDeServicos.GetRequiredService<IUniPlusBlindIndexService>().Should().BeOfType<LocalHmacBlindIndexService>();
    }

    [Theory]
    [InlineData("vault")]
    [InlineData("VAULT")]
    public void IUniPlusBlindIndexService_ProvedorVault_ResolveIndiceCegoDoVault(string provedorDeCifra)
    {
        using ServiceProvider provedorDeServicos = CriarProvedor(provedorDeCifra);

        provedorDeServicos.GetRequiredService<IUniPlusBlindIndexService>().Should().BeOfType<VaultTransitBlindIndexService>();
    }

    [Fact]
    public void IUniPlusEncryptionService_ResolvidoDuasVezes_DevolveAMesmaInstancia()
    {
        using ServiceProvider provedorDeServicos = CriarProvedor("local");

        provedorDeServicos.GetRequiredService<IUniPlusEncryptionService>()
            .Should().BeSameAs(provedorDeServicos.GetRequiredService<IUniPlusEncryptionService>());
    }

    [Fact]
    public void IUniPlusBlindIndexService_ProvedorInvalido_FalhaSoNaResolucaoComOTextoInformado()
    {
        using ServiceProvider provedorDeServicos = CriarProvedor("desconhecido");

        Action resolver = () => provedorDeServicos.GetRequiredService<IUniPlusBlindIndexService>();

        resolver.Should().Throw<OptionsValidationException>().WithMessage("*'desconhecido'*");
    }

    private static ServiceProvider CriarProvedor(string provedorDeCifra)
    {
        Dictionary<string, string?> valores = new()
        {
            ["UniPlus:Encryption:Provider"] = provedorDeCifra,
            ["UniPlus:Encryption:LocalKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["UniPlus:Encryption:VaultAddress"] = "http://vault.vault.svc:8200",
            ["UniPlus:Encryption:VaultToken"] = "hvs.dev",
        };
        IConfiguration configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        return new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(configuracao)
            .BuildServiceProvider();
    }
}
