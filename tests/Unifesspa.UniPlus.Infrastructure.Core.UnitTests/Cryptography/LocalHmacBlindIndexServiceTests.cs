namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

public sealed class LocalHmacBlindIndexServiceTests
{
    private static readonly byte[] ValidKey = new byte[32];
    private static readonly string ValidKeyBase64 = Convert.ToBase64String(ValidKey);

    private static LocalHmacBlindIndexService CriarServico(string? localKey = null) =>
        new(
            Options.Create(new EncryptionOptions { Provider = "local", LocalKey = localKey ?? ValidKeyBase64 }),
            NullLogger<LocalHmacBlindIndexService>.Instance);

    // ─── Determinismo (CA-01) ──────────────────────────────────────────────────

    [Fact]
    public async Task ComputarAsync_MesmoValorEMesmaChave_DeveDevolverOMesmoIndice()
    {
        LocalHmacBlindIndexService sut = CriarServico();
        byte[] valor = "valor-opaco-um"u8.ToArray();

        byte[] indice1 = await sut.ComputarAsync("uniplus-selecao-identificadores-hmac", valor);
        byte[] indice2 = await sut.ComputarAsync("uniplus-selecao-identificadores-hmac", valor);

        indice1.Should().Equal(indice2);
    }

    [Fact]
    public async Task ComputarAsync_MesmoValorComKeyNameDiferente_DeveDevolverIndiceDiferente()
    {
        LocalHmacBlindIndexService sut = CriarServico();
        byte[] valor = "valor-opaco-um"u8.ToArray();

        byte[] indiceA = await sut.ComputarAsync("uniplus-selecao-identificadores-hmac", valor);
        byte[] indiceB = await sut.ComputarAsync("uniplus-discentes-identificadores-hmac", valor);

        indiceA.Should().NotEqual(indiceB);
    }

    [Fact]
    public async Task ComputarAsync_ValoresDiferentes_DevemDevolverIndicesDiferentes()
    {
        LocalHmacBlindIndexService sut = CriarServico();

        byte[] indiceA = await sut.ComputarAsync("uniplus-selecao-identificadores-hmac", "valor-opaco-um"u8.ToArray());
        byte[] indiceB = await sut.ComputarAsync("uniplus-selecao-identificadores-hmac", "valor-opaco-dois"u8.ToArray());

        indiceA.Should().NotEqual(indiceB);
    }

    [Fact]
    public async Task ComputarAsync_LocalKeyDiferente_DeveDevolverIndiceDiferente()
    {
        byte[] valor = "valor-opaco-um"u8.ToArray();
        byte[] indiceA = await CriarServico().ComputarAsync("uniplus-selecao-identificadores-hmac", valor);

        string outraChave = Convert.ToBase64String(new byte[32].Select(static (_, i) => (byte)(i + 1)).ToArray());
        byte[] indiceB = await CriarServico(outraChave).ComputarAsync("uniplus-selecao-identificadores-hmac", valor);

        indiceA.Should().NotEqual(indiceB);
    }

    // ─── Chave inválida ──────────────────────────────────────────────────────

    [Fact]
    public void Construtor_ChaveAusente_DeveLancarInvalidOperationException()
    {
        Action ato = () => CriarServicoComOpcoes(new EncryptionOptions { Provider = "local", LocalKey = null });

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*UniPlus:Encryption:LocalKey*");
    }

    private static LocalHmacBlindIndexService CriarServicoComOpcoes(EncryptionOptions opts) =>
        new(Options.Create(opts), NullLogger<LocalHmacBlindIndexService>.Instance);

    [Fact]
    public void Construtor_ChaveComTamanhoErrado_DeveLancarInvalidOperationException()
    {
        string chave16Bytes = Convert.ToBase64String(new byte[16]);

        Action ato = () => CriarServico(localKey: chave16Bytes);

        ato.Should().Throw<InvalidOperationException>()
            .WithMessage("*32 bytes*");
    }
}
