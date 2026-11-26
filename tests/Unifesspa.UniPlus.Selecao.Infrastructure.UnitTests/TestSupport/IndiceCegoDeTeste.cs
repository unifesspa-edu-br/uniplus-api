namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>Índice cego que devolve sempre o mesmo valor, o que dá ao teste o vínculo coerente ou não com o envelope gravado.</summary>
internal sealed class IndiceCegoDeTeste : IUniPlusBlindIndexService
{
    public static readonly byte[] Vinculo = [1, 2, 3, 4];

    public Task<byte[]> ComputarAsync(string keyName, ReadOnlyMemory<byte> valor, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vinculo);
}
