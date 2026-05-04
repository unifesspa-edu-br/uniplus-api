namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>
/// Abstração de criptografia simétrica orientada a chaves nomeadas.
/// Implementações concretas: Vault transit engine (produção) e AES-GCM local (dev/CI).
/// </summary>
public interface IUniPlusEncryptionService
{
    /// <summary>Cifra <paramref name="plaintext"/> usando a chave <paramref name="keyName"/>.</summary>
    /// <exception cref="EncryptionFailureException">Qualquer falha na operação.</exception>
    Task<byte[]> EncryptAsync(string keyName, byte[] plaintext, CancellationToken cancellationToken = default);

    /// <summary>Decifra <paramref name="ciphertext"/> usando a chave <paramref name="keyName"/>.</summary>
    /// <exception cref="EncryptionFailureException">Qualquer falha na operação, incluindo adulteração.</exception>
    Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, CancellationToken cancellationToken = default);
}
