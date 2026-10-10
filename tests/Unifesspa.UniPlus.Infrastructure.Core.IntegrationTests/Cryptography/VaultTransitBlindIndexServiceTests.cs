namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Cryptography;

using AwesomeAssertions;

using DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(VaultContainerFixture.CollectionName)]
public sealed class VaultTransitBlindIndexServiceTests(VaultContainerFixture vault)
{
    private const string KeyName = "uniplus-test-hmac";

    private IUniPlusBlindIndexService CriarServico(int blindIndexKeyVersion = 1) =>
        new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(configure: opts =>
            {
                opts.Provider = "vault";
                opts.VaultAddress = vault.VaultAddress;
                opts.VaultToken = VaultContainerFixture.RootToken;
                opts.VaultTransitMount = VaultContainerFixture.TransitMount;
                opts.BlindIndexKeyVersion = blindIndexKeyVersion;
            })
            .BuildServiceProvider()
            .GetRequiredService<IUniPlusBlindIndexService>();

    // ─── Determinismo (CA-01) ──────────────────────────────────────────────────

    [Fact]
    public async Task ComputarAsync_MesmoValorEMesmaChave_DeveDevolverOMesmoIndice()
    {
        await vault.EnsureKeyExistsAsync(KeyName, keyType: "hmac");
        IUniPlusBlindIndexService sut = CriarServico();
        byte[] valor = "24843803480"u8.ToArray();

        byte[] indice1 = await sut.ComputarAsync(KeyName, valor);
        byte[] indice2 = await sut.ComputarAsync(KeyName, valor);

        indice1.Should().Equal(indice2);
    }

    [Fact]
    public async Task ComputarAsync_ChaveDiferente_DeveDevolverIndiceDiferente()
    {
        const string outraChave = "uniplus-test-hmac-outra";
        await vault.EnsureKeyExistsAsync(KeyName, keyType: "hmac");
        await vault.EnsureKeyExistsAsync(outraChave, keyType: "hmac");
        IUniPlusBlindIndexService sut = CriarServico();
        byte[] valor = "24843803480"u8.ToArray();

        byte[] indiceA = await sut.ComputarAsync(KeyName, valor);
        byte[] indiceB = await sut.ComputarAsync(outraChave, valor);

        indiceA.Should().NotEqual(indiceB);
    }

    // ─── Versão de chave fixa (CA-02) ──────────────────────────────────────────

    [Fact]
    public async Task ComputarAsync_ChaveGanhaVersaoNova_IndiceDaVersaoFixaNaoMuda()
    {
        await vault.EnsureKeyExistsAsync(KeyName, keyType: "hmac");
        IUniPlusBlindIndexService sutVersao1 = CriarServico(blindIndexKeyVersion: 1);
        byte[] valor = "24843803480"u8.ToArray();

        byte[] indiceAntes = await sutVersao1.ComputarAsync(KeyName, valor);

        await vault.RotateKeyAsync(KeyName);

        byte[] indiceDepois = await sutVersao1.ComputarAsync(KeyName, valor);

        indiceDepois.Should().Equal(indiceAntes);
    }

    [Fact]
    public async Task ComputarAsync_VersaoDeChaveDiferente_DeveDevolverIndiceDiferente()
    {
        await vault.EnsureKeyExistsAsync(KeyName, keyType: "hmac");
        await vault.RotateKeyAsync(KeyName);
        byte[] valor = "24843803480"u8.ToArray();

        byte[] indiceV1 = await CriarServico(blindIndexKeyVersion: 1).ComputarAsync(KeyName, valor);
        byte[] indiceV2 = await CriarServico(blindIndexKeyVersion: 2).ComputarAsync(KeyName, valor);

        indiceV1.Should().NotEqual(indiceV2);
    }
}
