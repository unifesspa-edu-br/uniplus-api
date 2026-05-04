namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

public sealed class LocalAesEncryptionServiceTests
{
    private static readonly byte[] ValidKey = new byte[32];
    private static readonly string ValidKeyBase64 = Convert.ToBase64String(ValidKey);

    private static LocalAesEncryptionService CriarServico(string? localKey = null) =>
        new(
            Options.Create(new EncryptionOptions { Provider = "local", LocalKey = localKey ?? ValidKeyBase64 }),
            NullLogger<LocalAesEncryptionService>.Instance);

    // ─── Round-trip ──────────────────────────────────────────────────────────

    [Fact]
    public async Task EncryptAsync_QuandoDecryptAsync_DeveRetornarPlaintextOriginal()
    {
        LocalAesEncryptionService sut = CriarServico();
        byte[] plaintext = "UniPlus — dado sensível"u8.ToArray();

        byte[] ciphertext = await sut.EncryptAsync("cursor", plaintext);
        byte[] resultado = await sut.DecryptAsync("cursor", ciphertext);

        resultado.Should().Equal(plaintext);
    }

    // ─── IV aleatório ────────────────────────────────────────────────────────

    [Fact]
    public async Task EncryptAsync_MesmoPlaintext_DeveProduzirCiphertextsDiferentes()
    {
        LocalAesEncryptionService sut = CriarServico();
        byte[] plaintext = "mesmo conteúdo"u8.ToArray();

        byte[] ct1 = await sut.EncryptAsync("cursor", plaintext);
        byte[] ct2 = await sut.EncryptAsync("cursor", plaintext);

        ct1.Should().NotEqual(ct2);
    }

    // ─── Tamper detection ────────────────────────────────────────────────────

    [Fact]
    public async Task DecryptAsync_CiphertextAdulterado_DeveLancarEncryptionFailureException()
    {
        LocalAesEncryptionService sut = CriarServico();
        byte[] plaintext = "dado a proteger"u8.ToArray();
        byte[] ciphertext = await sut.EncryptAsync("cursor", plaintext);

        // Adulterar o último byte dos dados cifrados
        ciphertext[^1] ^= 0xFF;

        Func<Task> ato = () => sut.DecryptAsync("cursor", ciphertext);

        await ato.Should().ThrowAsync<EncryptionFailureException>()
            .Where(e => e.KeyName == "cursor");
    }

    [Fact]
    public async Task DecryptAsync_CiphertextMuitoCurto_DeveLancarEncryptionFailureException()
    {
        LocalAesEncryptionService sut = CriarServico();
        byte[] ciphertextInvalido = new byte[10];

        Func<Task> ato = () => sut.DecryptAsync("cursor", ciphertextInvalido);

        await ato.Should().ThrowAsync<EncryptionFailureException>();
    }

    // ─── Chave inválida ──────────────────────────────────────────────────────

    [Fact]
    public void Construtor_ChaveAusente_DeveLancarInvalidOperationException()
    {
        Action ato = () => CriarServicoComOpcoes(new EncryptionOptions { Provider = "local", LocalKey = null });

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*UniPlus:Encryption:LocalKey*");
    }

    private static LocalAesEncryptionService CriarServicoComOpcoes(EncryptionOptions opts) =>
        new(Options.Create(opts), NullLogger<LocalAesEncryptionService>.Instance);

    [Fact]
    public void Construtor_ChaveBase64Invalida_DeveLancarInvalidOperationException()
    {
        Action ato = () => CriarServico(localKey: "não-é-base64!!!");

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*Base64*");
    }

    [Fact]
    public void Construtor_ChaveComTamanhoErrado_DeveLancarInvalidOperationException()
    {
        string chave16Bytes = Convert.ToBase64String(new byte[16]);

        Action ato = () => CriarServico(localKey: chave16Bytes);

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*32 bytes*");
    }
}
