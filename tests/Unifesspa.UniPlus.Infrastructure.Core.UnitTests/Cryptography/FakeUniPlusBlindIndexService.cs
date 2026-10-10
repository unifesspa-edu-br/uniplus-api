namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using System.Security.Cryptography;
using System.Text;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>
/// Fake determinístico de <see cref="IUniPlusBlindIndexService"/> para testes unitários de
/// handlers e repositórios que precisam de um índice estável sem subir Vault nem derivar
/// chave real — só SHA-256 de <c>keyName + ":" + valor</c>. Nunca usar fora de teste: não
/// tem segredo nenhum, qualquer um recalcula o índice a partir do valor em claro.
/// </summary>
public sealed class FakeUniPlusBlindIndexService : IUniPlusBlindIndexService
{
    public Task<byte[]> ComputarAsync(string keyName, ReadOnlyMemory<byte> valor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);

        byte[] prefixo = Encoding.UTF8.GetBytes(keyName + ":");
        byte[] entrada = [.. prefixo, .. valor.ToArray()];
        return Task.FromResult(SHA256.HashData(entrada));
    }
}
