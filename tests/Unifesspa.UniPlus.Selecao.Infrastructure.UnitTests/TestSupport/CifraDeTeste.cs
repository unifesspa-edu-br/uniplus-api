namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using System.Text;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>Serviço de cifra que devolve sempre o mesmo conteúdo decifrado, qualquer que seja o envelope recebido.</summary>
internal sealed class CifraDeTeste(byte[] decifrado) : IUniPlusEncryptionService
{
    public CifraDeTeste(string textoDecifrado)
        : this(Encoding.UTF8.GetBytes(textoDecifrado))
    {
    }

    public Task<byte[]> EncryptAsync(string keyName, byte[] plaintext, CancellationToken cancellationToken = default) =>
        Task.FromResult(plaintext);

    public Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, CancellationToken cancellationToken = default) =>
        Task.FromResult(decifrado);
}
