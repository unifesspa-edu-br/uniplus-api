namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(VaultContainerFixture.CollectionName)]
public sealed class VaultTransitEncryptionServiceTests(VaultContainerFixture vault)
{
    private const string KeyName = "uniplus-test";

    private IUniPlusEncryptionService CriarServico() =>
        new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(configure: opts =>
            {
                opts.Provider = "vault";
                opts.VaultAddress = vault.VaultAddress;
                opts.VaultToken = VaultContainerFixture.RootToken;
                opts.VaultTransitMount = VaultContainerFixture.TransitMount;
            })
            .BuildServiceProvider()
            .GetRequiredService<IUniPlusEncryptionService>();

    // ─── Round-trip ──────────────────────────────────────────────────────────

    [Fact]
    public async Task EncryptAsync_QuandoDecryptAsync_DeveRetornarPlaintextOriginal()
    {
        await vault.EnsureKeyExistsAsync(KeyName);
        IUniPlusEncryptionService sut = CriarServico();
        byte[] plaintext = "UniPlus — dado sensível"u8.ToArray();

        byte[] ciphertext = await sut.EncryptAsync(KeyName, plaintext);
        byte[] resultado = await sut.DecryptAsync(KeyName, ciphertext);

        resultado.Should().Equal(plaintext);
    }

    // ─── Plaintext vazio ─────────────────────────────────────────────────────

    [Fact]
    public async Task EncryptAsync_PlaintextVazio_DeveRoundTripCorretamente()
    {
        await vault.EnsureKeyExistsAsync(KeyName);
        IUniPlusEncryptionService sut = CriarServico();
        byte[] plaintext = [];

        byte[] ciphertext = await sut.EncryptAsync(KeyName, plaintext);
        byte[] resultado = await sut.DecryptAsync(KeyName, ciphertext);

        resultado.Should().BeEmpty();
    }

    // ─── Ciphertext de chave diferente rejeitado pelo Vault ──────────────────

    [Fact]
    public async Task DecryptAsync_CiphertextDeChaveDiferente_DeveLancarEncryptionFailureException()
    {
        const string outraChave = "uniplus-outra";
        await vault.EnsureKeyExistsAsync(KeyName);
        await vault.EnsureKeyExistsAsync(outraChave);

        IUniPlusEncryptionService sut = CriarServico();
        byte[] plaintext = "dado sensível"u8.ToArray();

        byte[] ciphertext = await sut.EncryptAsync(KeyName, plaintext);

        Func<Task> ato = () => sut.DecryptAsync(outraChave, ciphertext);

        await ato.Should().ThrowAsync<EncryptionFailureException>()
            .Where(e => e.KeyName == outraChave);
    }

    // ─── Token inválido → EncryptionFailureException (retry esgotado) ────────

    [Fact]
    public async Task EncryptAsync_TokenInvalido_DeveLancarEncryptionFailureException()
    {
        await vault.EnsureKeyExistsAsync(KeyName);

        IUniPlusEncryptionService sut = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(configure: opts =>
            {
                opts.Provider = "vault";
                opts.VaultAddress = vault.VaultAddress;
                opts.VaultToken = "token-invalido";
                opts.VaultTransitMount = VaultContainerFixture.TransitMount;
            })
            .BuildServiceProvider()
            .GetRequiredService<IUniPlusEncryptionService>();

        Func<Task> ato = () => sut.EncryptAsync(KeyName, "payload"u8.ToArray());

        await ato.Should().ThrowAsync<EncryptionFailureException>();
    }
}
